using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;

namespace Pw.Hub.Behaviors
{
    /// <summary>
    /// Поведение для RichTextBox, которое принимает список строк unified diff и рендерит их
    /// с цветовой индикацией добавлений/удалений/контекста.
    /// </summary>
    public static class RichTextBoxDiffBehavior
    {
        public static readonly DependencyProperty LinesProperty = DependencyProperty.RegisterAttached(
            "Lines",
            typeof(IList<string>),
            typeof(RichTextBoxDiffBehavior),
            new PropertyMetadata(null, OnLinesChanged));

        public static void SetLines(DependencyObject element, IList<string> value) => element.SetValue(LinesProperty, value);
        public static IList<string> GetLines(DependencyObject element) => (IList<string>)element.GetValue(LinesProperty);

        private static void OnLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RichTextBox rtb) return;
            try
            {
                var doc = new FlowDocument { PagePadding = new Thickness(4) };
                var mono = Application.Current.TryFindResource("MonospaceFont") as FontFamily ?? new FontFamily("Consolas");
                doc.FontFamily = mono;
                doc.FontSize = 13;

                var lines = e.NewValue as IList<string> ?? Array.Empty<string>();
                foreach (var line in lines)
                {
                    var p = new Paragraph { Margin = new Thickness(0), LineHeight = 16 };
                    var run = new Run(line ?? string.Empty);

                    if (line.StartsWith("+++ ") || line.StartsWith("--- ") || line.StartsWith("@@"))
                    {
                        run.Foreground = Application.Current.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
                        run.FontWeight = FontWeights.SemiBold;
                    }
                    else if (line.StartsWith("+"))
                    {
                        run.Foreground = Brushes.Green;
                    }
                    else if (line.StartsWith("-"))
                    {
                        run.Foreground = Brushes.IndianRed;
                    }
                    else
                    {
                        run.Foreground = Application.Current.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
                    }

                    p.Inlines.Add(run);
                    doc.Blocks.Add(p);
                }

                if (doc.Blocks.Count == 0)
                {
                    doc.Blocks.Add(new Paragraph(new Run("")) { Margin = new Thickness(0) });
                }

                rtb.Document = doc;
            }
            catch
            {
                try { rtb.Document = new FlowDocument(new Paragraph(new Run("Не удалось отобразить diff"))); } catch { }
            }
        }
    }
}
