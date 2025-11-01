using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using Pw.Hub.Windows; // BreakpointBackgroundRenderer

namespace Pw.Hub.Behaviors
{
    /// <summary>
    /// Attached behavior that manages editor breakpoints and visuals, syncing with a VM collection.
    /// </summary>
    public static class EditorBreakpointsBehavior
    {
        public static readonly DependencyProperty BreakpointsProperty = DependencyProperty.RegisterAttached(
            "Breakpoints",
            typeof(ObservableCollection<int>),
            typeof(EditorBreakpointsBehavior),
            new PropertyMetadata(null, OnBreakpointsChanged));

        public static void SetBreakpoints(DependencyObject element, ObservableCollection<int> value) => element.SetValue(BreakpointsProperty, value);
        public static ObservableCollection<int> GetBreakpoints(DependencyObject element) => (ObservableCollection<int>)element.GetValue(BreakpointsProperty);

        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "_State",
            typeof(BehaviorState),
            typeof(EditorBreakpointsBehavior));

        private static void OnBreakpointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextEditor editor) return;

            // Detach previous
            var oldState = (BehaviorState)editor.GetValue(StateProperty);
            oldState?.Detach(editor);

            var collection = e.NewValue as ObservableCollection<int>;
            if (collection == null) { editor.SetValue(StateProperty, null); return; }

            var state = new BehaviorState(collection);
            state.Attach(editor);
            editor.SetValue(StateProperty, state);
        }

        private sealed class BehaviorState
        {
            private readonly ObservableCollection<int> _vmCollection;
            private readonly HashSet<int> _set = new();
            private BreakpointBackgroundRenderer _renderer;

            public BehaviorState(ObservableCollection<int> vmCollection)
            {
                _vmCollection = vmCollection;
                foreach (var i in vmCollection.Distinct()) _set.Add(i);
            }

            public void Attach(TextEditor editor)
            {
                // Visual markers
                try
                {
                    _renderer = new BreakpointBackgroundRenderer(_set);
                    editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
                    editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
                }
                catch { }

                // Sync: VM -> set
                _vmCollection.CollectionChanged += VmCollectionOnChanged;

                // Handle F9
                editor.PreviewKeyDown += OnEditorKeyDown;
            }

            public void Detach(TextEditor editor)
            {
                try
                {
                    // Unsubscribe
                    _vmCollection.CollectionChanged -= VmCollectionOnChanged;
                    editor.PreviewKeyDown -= OnEditorKeyDown;

                    if (_renderer != null)
                    {
                        editor.TextArea.TextView.BackgroundRenderers.Remove(_renderer);
                        editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
                    }
                }
                catch { }
            }

            private void VmCollectionOnChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                try
                {
                    if (e.Action == NotifyCollectionChangedAction.Reset)
                    {
                        _set.Clear();
                    }
                    else
                    {
                        if (e.OldItems != null)
                            foreach (int i in e.OldItems) _set.Remove(i);
                        if (e.NewItems != null)
                            foreach (int i in e.NewItems) _set.Add(i);
                    }
                }
                catch { }
            }

            private void OnEditorKeyDown(object? sender, KeyEventArgs e)
            {
                if (e.Key == Key.F9)
                {
                    if (sender is not TextEditor ed) return;
                    try
                    {
                        var line = ed.TextArea.Caret.Line;
                        if (_set.Contains(line))
                        {
                            _set.Remove(line);
                            _vmCollection.Remove(line);
                        }
                        else
                        {
                            _set.Add(line);
                            if (!_vmCollection.Contains(line)) _vmCollection.Add(line);
                        }
                        try { ed.TextArea.TextView.InvalidateLayer(KnownLayer.Background); } catch { }
                    }
                    catch { }
                    finally { e.Handled = true; }
                }
            }
        }
    }
}