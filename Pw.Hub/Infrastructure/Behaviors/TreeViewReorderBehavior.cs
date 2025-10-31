using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Pw.Hub.Models;
using Pw.Hub.Services;
using Pw.Hub.ViewModels;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.Infrastructure.Behaviors;

/// <summary>
/// Attached Behavior для drag&drop реордера узлов TreeView (отряды/аккаунты).
/// Вся логика DnD вынесена из MainWindow в это поведение. Сохранение порядка выполняет IOrderingService.
/// Представление остаётся «тонким» — достаточно включить поведение в XAML: beh:TreeViewReorderBehavior.Enabled="True".
/// </summary>
public static class TreeViewReorderBehavior
{
    // Внутреннее состояние DnD
    private class State
    {
        public Point DragStartPoint;
        public Account DraggedAccount;
        public Squad DraggedSquad;

        public TreeViewItem AdornerItem;
        public InsertionAdorner InsertionAdorner;

        public Account DropTargetAccount;
        public bool InsertAbove;

        public Squad DropTargetSquad;
        public bool InsertAboveSquad;
    }

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(TreeViewReorderBehavior),
        new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeView tree) return;
        if ((bool)e.NewValue)
        {
            var st = new State();
            tree.Tag = st; // хранить состояние на дереве
            tree.AllowDrop = true;
            tree.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            tree.PreviewMouseMove += OnPreviewMouseMove;
            tree.DragOver += OnDragOver;
            tree.Drop += OnDrop;
            tree.DragLeave += OnDragLeave;
            tree.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        }
        else
        {
            tree.AllowDrop = false;
            tree.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            tree.PreviewMouseMove -= OnPreviewMouseMove;
            tree.DragOver -= OnDragOver;
            tree.Drop -= OnDrop;
            tree.DragLeave -= OnDragLeave;
            tree.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            if (tree.Tag is State st)
            {
                ClearInsertionAdorner(tree, st);
                tree.Tag = null;
            }
        }
    }

    // === Handlers ===

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var tree = (TreeView)sender;
            var st = (State)tree.Tag;
            st.DragStartPoint = e.GetPosition(tree);
            st.DraggedAccount = null;
            st.DraggedSquad = null;

            // Найти TreeViewItem под курсором и определить тип данных
            var tvi = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (tvi?.DataContext is Account acc) st.DraggedAccount = acc;
            else if (tvi?.DataContext is Squad sq) st.DraggedSquad = sq;
        }
        catch { }
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            var tree = (TreeView)sender;
            var st = (State)tree.Tag;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (st.DraggedAccount == null && st.DraggedSquad == null) return;

            var pos = e.GetPosition(tree);
            var diff = new Vector(Math.Abs(pos.X - st.DragStartPoint.X), Math.Abs(pos.Y - st.DragStartPoint.Y));
            if (diff.X < SystemParameters.MinimumHorizontalDragDistance && diff.Y < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (st.DraggedAccount != null)
                DragDrop.DoDragDrop(tree, new DataObject(typeof(Account), st.DraggedAccount), DragDropEffects.Move);
            else if (st.DraggedSquad != null)
                DragDrop.DoDragDrop(tree, new DataObject(typeof(Squad), st.DraggedSquad), DragDropEffects.Move);
        }
        catch { }
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        try
        {
            var tree = (TreeView)sender;
            var st = (State)tree.Tag;

            // Account drag
            if (e.Data.GetDataPresent(typeof(Account)))
            {
                var dragged = (Account)e.Data.GetData(typeof(Account));
                if (dragged == null) { e.Effects = DragDropEffects.None; e.Handled = true; ClearInsertionAdorner(tree, st); return; }

                var target = GetAccountOrSquadUnderMouse(tree, e.OriginalSource as DependencyObject);
                if (target.squad == null) { e.Effects = DragDropEffects.None; e.Handled = true; ClearInsertionAdorner(tree, st); return; }

                e.Effects = DragDropEffects.Move;

                if (target.account != null)
                {
                    var squadItem = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(target.squad);
                    TreeViewItem accItem = null;
                    if (squadItem != null)
                        accItem = (TreeViewItem)squadItem.ItemContainerGenerator.ContainerFromItem(target.account);
                    if (accItem != null)
                    {
                        var pos = e.GetPosition(accItem);
                        var above = pos.Y < accItem.ActualHeight / 2;
                        st.DropTargetAccount = target.account;
                        st.InsertAbove = above;
                        UpdateInsertionAdorner(tree, st, accItem, above);
                    }
                    else ClearInsertionAdorner(tree, st);
                }
                else
                {
                    // dropping into squad tail
                    var vm = tree.DataContext as MainViewModel;
                    var squadVm = vm?.Squads.FirstOrDefault(s => s.Id == target.squad.Id);
                    if (squadVm != null && squadVm.Accounts.Count > 0)
                    {
                        var lastAcc = squadVm.Accounts.Last();
                        var squadItem = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(target.squad);
                        var lastItem = (TreeViewItem)squadItem?.ItemContainerGenerator.ContainerFromItem(lastAcc);
                        if (lastItem != null)
                        {
                            st.DropTargetAccount = lastAcc;
                            st.InsertAbove = false;
                            UpdateInsertionAdorner(tree, st, lastItem, false);
                        }
                        else ClearInsertionAdorner(tree, st);
                    }
                    else ClearInsertionAdorner(tree, st);
                }
                e.Handled = true; return;
            }

            // Squad drag
            if (e.Data.GetDataPresent(typeof(Squad)))
            {
                // find squad item under mouse
                var targetItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
                Squad targetSquad = targetItem?.DataContext as Squad;

                e.Effects = DragDropEffects.Move;
                if (targetItem != null && targetSquad != null)
                {
                    var pos = e.GetPosition(targetItem);
                    var above = pos.Y < targetItem.ActualHeight / 2;
                    st.DropTargetSquad = targetSquad;
                    st.InsertAboveSquad = above;
                    UpdateInsertionAdorner(tree, st, targetItem, above);
                }
                else
                {
                    // show below last squad
                    var vm = tree.DataContext as MainViewModel;
                    if (vm?.Squads.Count > 0)
                    {
                        var last = vm.Squads.Last();
                        var lastItem = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(last);
                        if (lastItem != null)
                        {
                            st.DropTargetSquad = last;
                            st.InsertAboveSquad = false;
                            UpdateInsertionAdorner(tree, st, lastItem, false);
                        }
                        else ClearInsertionAdorner(tree, st);
                    }
                    else ClearInsertionAdorner(tree, st);
                }
                e.Handled = true; return;
            }

            e.Effects = DragDropEffects.None; e.Handled = true; ClearInsertionAdorner(tree, st);
        }
        catch { }
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        var tree = (TreeView)sender;
        var st = (State)tree.Tag;
        try
        {
            // Account drop
            if (e.Data.GetDataPresent(typeof(Account)))
            {
                var dragged = (Account)e.Data.GetData(typeof(Account));
                if (dragged == null) { ClearInsertionAdorner(tree, st); return; }

                var target = GetAccountOrSquadUnderMouse(tree, e.OriginalSource as DependencyObject);
                if (target.squad == null) { ClearInsertionAdorner(tree, st); return; }

                var vm = tree.DataContext as MainViewModel;
                var sourceSquadVm = vm?.Squads.FirstOrDefault(s => s.Accounts.Any(a => a.Id == dragged.Id));
                var targetSquadVm = vm?.Squads.FirstOrDefault(s => s.Id == (target.account?.SquadId ?? target.squad.Id));
                if (sourceSquadVm == null || targetSquadVm == null) { ClearInsertionAdorner(tree, st); return; }

                var targetList = targetSquadVm.Accounts;
                int anchorIndex;
                if (st.DropTargetAccount != null)
                {
                    var anchor = targetList.FirstOrDefault(a => a.Id == st.DropTargetAccount.Id);
                    anchorIndex = anchor != null ? targetList.IndexOf(anchor) : targetList.Count - 1;
                }
                else if (target.account != null)
                {
                    anchorIndex = targetList.IndexOf(targetList.First(a => a.Id == target.account.Id));
                }
                else anchorIndex = targetList.Count - 1;

                int newIndex = st.InsertAbove ? anchorIndex : anchorIndex + 1;
                if (newIndex < 0) newIndex = 0;
                if (newIndex > targetList.Count) newIndex = targetList.Count;

                var sourceList = sourceSquadVm.Accounts;
                var sourceIdx = sourceList.IndexOf(sourceList.First(a => a.Id == dragged.Id));
                if (sourceIdx < 0) { ClearInsertionAdorner(tree, st); return; }

                if (ReferenceEquals(sourceList, targetList) && sourceIdx < newIndex) newIndex--;
                sourceList.RemoveAt(sourceIdx);
                newIndex = Math.Max(0, Math.Min(targetList.Count, newIndex));
                targetList.Insert(newIndex, dragged);

                // Update model to new squad if changed
                var movedAcrossSquads = sourceSquadVm.Id != targetSquadVm.Id;
                if (movedAcrossSquads) dragged.SquadId = targetSquadVm.Id;

                // Persist via ordering service
                var ordering = (App.Services.GetService(typeof(IOrderingService)) as IOrderingService) ?? new OrderingService();
                var targetAccForService = st.DropTargetAccount ?? target.account;
                var insertAfterAccount = !st.InsertAbove;
                _ = ordering.ReorderAccountAsync(dragged, targetSquadVm, targetAccForService, insertAfterAccount);

                e.Handled = true;
                return;
            }

            // Squad drop
            if (e.Data.GetDataPresent(typeof(Squad)))
            {
                var draggedSquad = (Squad)e.Data.GetData(typeof(Squad));
                if (draggedSquad == null) { ClearInsertionAdorner(tree, st); return; }

                var vm = tree.DataContext as MainViewModel;
                var list = vm?.Squads;
                if (list == null) { ClearInsertionAdorner(tree, st); return; }

                var oldIndex = list.IndexOf(list.First(s => s.Id == draggedSquad.Id));
                if (oldIndex < 0) { ClearInsertionAdorner(tree, st); return; }

                int anchorIndex = -1;
                if (st.DropTargetSquad != null)
                {
                    var anchor = list.FirstOrDefault(s => s.Id == st.DropTargetSquad.Id);
                    anchorIndex = anchor != null ? list.IndexOf(anchor) : list.Count - 1;
                }
                else
                {
                    var dep = e.OriginalSource as DependencyObject;
                    var tvi = FindAncestor<TreeViewItem>(dep);
                    if (tvi?.DataContext is Squad sq)
                        anchorIndex = list.IndexOf(list.First(s => s.Id == sq.Id));
                    else anchorIndex = list.Count - 1;
                }

                int newIndex = st.InsertAboveSquad ? anchorIndex : anchorIndex + 1;
                if (oldIndex < newIndex) newIndex--;
                newIndex = Math.Max(0, Math.Min(list.Count - 1, newIndex));
                if (newIndex != oldIndex) list.Move(oldIndex, newIndex);

                var ordering = (App.Services.GetService(typeof(IOrderingService)) as IOrderingService) ?? new OrderingService();
                if (st.DropTargetSquad != null)
                {
                    var insertAfter = !st.InsertAboveSquad;
                    _ = ordering.ReorderSquadAsync(draggedSquad, st.DropTargetSquad, insertAfter);
                }
                else if (list.Count > 0)
                {
                    var anchor = list[Math.Min(newIndex, list.Count - 1)];
                    var insertAfter = newIndex > list.IndexOf(anchor);
                    _ = ordering.ReorderSquadAsync(draggedSquad, anchor, insertAfter);
                }
                e.Handled = true;
                return;
            }
        }
        catch { }
        finally
        {
            ClearInsertionAdorner(tree, st);
        }
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        try
        {
            var tree = (TreeView)sender;
            var st = (State)tree.Tag;
            ClearInsertionAdorner(tree, st);
        }
        catch { }
    }

    private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // only UI selection logic should remain in MainWindow; here we do nothing special
    }

    // === Helpers ===

    private static (Squad squad, Account account) GetAccountOrSquadUnderMouse(TreeView tree, DependencyObject element)
    {
        try
        {
            var tvi = FindAncestor<TreeViewItem>(element);
            if (tvi != null)
            {
                if (tvi.DataContext is Account acc)
                {
                    var vm = tree.DataContext as MainViewModel;
                    var squad = vm?.Squads.FirstOrDefault(s => s.Id == acc.SquadId);
                    return (squad, acc);
                }
                if (tvi.DataContext is Squad sq) return (sq, null);
            }
        }
        catch { }
        return (null, null);
    }

    private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null && current is not T)
            current = VisualTreeHelper.GetParent(current);
        return current as T;
    }

    private static void UpdateInsertionAdorner(TreeView tree, State st, TreeViewItem item, bool above)
    {
        try
        {
            if (item == null) { ClearInsertionAdorner(tree, st); return; }
            if (!ReferenceEquals(st.AdornerItem, item) || st.InsertionAdorner == null)
            {
                ClearInsertionAdorner(tree, st);
                st.AdornerItem = item;
                var layer = AdornerLayer.GetAdornerLayer(item);
                if (layer != null)
                {
                    st.InsertionAdorner = new InsertionAdorner(item) { PositionAbove = above };
                    layer.Add(st.InsertionAdorner);
                }
            }
            else
            {
                st.InsertionAdorner.PositionAbove = above;
                st.InsertionAdorner.InvalidateVisual();
            }
        }
        catch { }
    }

    private static void ClearInsertionAdorner(ItemsControl owner, State st)
    {
        try
        {
            if (st.AdornerItem != null && st.InsertionAdorner != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(st.AdornerItem);
                layer?.Remove(st.InsertionAdorner);
            }
        }
        catch { }
        finally
        {
            st.InsertionAdorner = null;
            st.AdornerItem = null;
            st.DropTargetAccount = null;
            st.DropTargetSquad = null;
            st.InsertAboveSquad = false;
        }
    }
}
