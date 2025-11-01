using System;
using System.Windows;
using System.Windows.Controls;

namespace Pw.Hub.Behaviors
{
    /// <summary>
    /// Автопрокрутка TextBox/ScrollViewer к концу при изменении текста.
    /// Предназначено для логов/консолей вывода.
    /// </summary>
    public static class AutoScrollBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AutoScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox tb)
            {
                if ((bool)e.NewValue)
                {
                    tb.TextChanged += TbOnTextChanged;
                }
                else
                {
                    tb.TextChanged -= TbOnTextChanged;
                }
            }
            else if (d is ScrollViewer sv)
            {
                // nothing: we scroll it from TextBox handler when possible
            }
        }

        private static void TbOnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            try
            {
                // Переносим выполнение после разметки, чтобы гарантировать корректную прокрутку
                if (!tb.Dispatcher.CheckAccess())
                {
                    tb.Dispatcher.BeginInvoke(new Action<object, TextChangedEventArgs>(TbOnTextChanged), System.Windows.Threading.DispatcherPriority.Background, sender, e);
                    return;
                }

                tb.CaretIndex = tb.Text?.Length ?? 0;
                tb.UpdateLayout();
                tb.ScrollToEnd();

                // Если TextBox обёрнут во внешнюю ScrollViewer — прокручиваем и её
                var parent = FindParentScrollViewer(tb);
                try { parent?.ScrollToBottom(); } catch { }
            }
            catch { }
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject child)
        {
            var p = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (p != null)
            {
                if (p is ScrollViewer sv) return sv;
                p = System.Windows.Media.VisualTreeHelper.GetParent(p);
            }
            return null;
        }
    }
}
