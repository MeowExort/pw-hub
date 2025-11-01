using System;
using System.Windows;
using System.Windows.Data;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace Pw.Hub.Behaviors
{
    /// <summary>
    /// Enables two-way binding of AvalonEdit TextEditor.Text to a view-model string property.
    /// </summary>
    public static class AvalonEditTextBehavior
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(AvalonEditTextBehavior),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);
        public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextEditor editor) return;

            // Detach event first to avoid recursion
            editor.TextChanged -= EditorOnTextChanged;

            var newText = e.NewValue as string ?? string.Empty;
            if (!string.Equals(editor.Text, newText, StringComparison.Ordinal))
            {
                // Avoid losing undo stack: update the underlying document
                if (editor.Document == null) editor.Document = new TextDocument();
                editor.Document.Text = newText;
            }

            editor.TextChanged += EditorOnTextChanged;
        }

        private static void EditorOnTextChanged(object? sender, EventArgs e)
        {
            if (sender is not TextEditor editor) return;

            var binding = BindingOperations.GetBindingExpression(editor, TextProperty);
            if (binding != null)
            {
                // push current editor text back to the bound VM property
                SetText(editor, editor.Text);
                binding.UpdateSource();
            }
        }
    }
}