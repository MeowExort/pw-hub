using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Markdig;
using Microsoft.Web.WebView2.Core;
using Pw.Hub.Services;

namespace Pw.Hub.Windows
{
    public partial class ModulesApiEditorWindow : Window
    {
        private readonly ModuleDto? _existing;
        private readonly ObservableCollection<InputItem> _inputs = new();
        private CancellationTokenSource? _previewCts;

        public ModulesApiEditorWindow(ModuleDto? existing = null)
        {
            InitializeComponent();
            _existing = existing;

            // Bind grid
            InputsGrid.ItemsSource = _inputs;

            Loaded += async (_, _) => await InitPreviewAsync();

            if (existing != null)
            {
                NameText.Text = existing.Name;
                VersionText.Text = string.IsNullOrWhiteSpace(existing.Version) ? "1.0.0" : existing.Version;
                DescriptionEditor.Text = existing.Description ?? string.Empty;
                ScriptText.Text = existing.Script;
                foreach (var i in existing.Inputs ?? Array.Empty<InputDefinitionDto>())
                {
                    _inputs.Add(new InputItem
                    {
                        Name = i.Name ?? string.Empty,
                        Label = string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label,
                        Type = string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type,
                        Default = string.Empty,
                        Required = i.Required
                    });
                }
                Title = $"Редактирование: {existing.Name}";
            }
        }

        private async Task InitPreviewAsync()
        {
            try
            {
                if (PreviewWebView.CoreWebView2 == null)
                {
                    await PreviewWebView.EnsureCoreWebView2Async();
                }
            }
            catch { }
            await UpdatePreviewAsync();
            UpdatePreviewVisibility();
        }

        public CreateOrUpdateModule GetRequest()
        {
            var inputs = _inputs
                .Select(i => new InputDefinitionDto
                {
                    Name = i.Name?.Trim() ?? string.Empty,
                    Label = string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label,
                    Type = string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type,
                    Required = i.Required
                })
                .ToArray();

            return new CreateOrUpdateModule
            {
                Name = NameText.Text?.Trim() ?? string.Empty,
                Version = string.IsNullOrWhiteSpace(VersionText.Text) ? "1.0.0" : VersionText.Text.Trim(),
                Description = DescriptionEditor.Text,
                Script = ScriptText.Text ?? string.Empty,
                Inputs = inputs
            };
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameText.Text) || string.IsNullOrWhiteSpace(ScriptText.Text))
            {
                MessageBox.Show(this, "Имя и скрипт обязательны");
                return;
            }
            DialogResult = true;
        }

        private void AddInput_Click(object sender, RoutedEventArgs e)
        {
            _inputs.Add(new InputItem { Name = "param", Label = "Параметр", Type = "string", Default = string.Empty, Required = false });
        }

        private void RemoveInput_Click(object sender, RoutedEventArgs e)
        {
            if (InputsGrid.SelectedItem is InputItem item)
            {
                _inputs.Remove(item);
            }
        }

        private void DescriptionEditor_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            DebouncePreview();
        }

        private void ShowPreviewCheck_OnChanged(object sender, RoutedEventArgs e)
        {
            UpdatePreviewVisibility();
            if (ShowPreviewCheck.IsChecked == true)
                DebouncePreview();
        }

        private void UpdatePreviewVisibility()
        {
            try
            {
                // If controls are not yet initialized (can happen during InitializeComponent event firing), skip safely
                if (PreviewContainer == null || ShowPreviewCheck == null)
                {
                    // try again after layout is ready
                    Dispatcher.BeginInvoke(new Action(UpdatePreviewVisibility));
                    return;
                }
                var visible = ShowPreviewCheck.IsChecked == true;
                PreviewContainer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private void DebouncePreview()
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    if (!token.IsCancellationRequested)
                    {
                        await Dispatcher.InvokeAsync(async () => await UpdatePreviewAsync());
                    }
                }
                catch { }
            });
        }

        private async Task UpdatePreviewAsync()
        {
            try
            {
                var md = DescriptionEditor.Text ?? string.Empty;
                var html = string.IsNullOrWhiteSpace(md) ? "<i>Нет описания</i>" : Markdown.ToHtml(md);
                if (PreviewWebView.CoreWebView2 == null)
                {
                    await PreviewWebView.EnsureCoreWebView2Async();
                }
                var doc = "<!DOCTYPE html><html><head><meta charset='utf-8'><style>body{font-family:Segoe UI,Arial,sans-serif;padding:12px} pre{background:#f6f8fa;padding:8px;border-radius:6px;overflow:auto} code{background:#f6f8fa;padding:2px 4px;border-radius:4px}</style></head><body>" + html + "</body></html>";
                PreviewWebView.NavigateToString(doc);
            }
            catch { }
        }

        // Toolbar helpers
        private void Bold_Click(object sender, RoutedEventArgs e) => WrapSelection("**", "**");
        private void Italic_Click(object sender, RoutedEventArgs e) => WrapSelection("*", "*");
        private void Header_Click(object sender, RoutedEventArgs e) => PrefixSelection("# ");
        private void List_Click(object sender, RoutedEventArgs e) => PrefixEachLine("- ");
        private void Code_Click(object sender, RoutedEventArgs e) => WrapSelection("````n", "\n```");

        private void WrapSelection(string left, string right)
        {
            try
            {
                var tb = DescriptionEditor;
                var sel = tb.SelectedText;
                if (string.IsNullOrEmpty(sel)) sel = "текст";
                var insert = left + sel + right;
                var start = tb.SelectionStart;
                tb.SelectedText = insert;
                tb.SelectionStart = start + left.Length;
                tb.SelectionLength = sel.Length;
            }
            catch { }
        }

        private void PrefixSelection(string prefix)
        {
            try
            {
                var tb = DescriptionEditor;
                var lineStart = tb.Text.LastIndexOf('\n', Math.Max(0, tb.SelectionStart - 1)) + 1;
                tb.Select(lineStart, 0);
                tb.SelectedText = prefix;
            }
            catch { }
        }

        private void PrefixEachLine(string prefix)
        {
            try
            {
                var tb = DescriptionEditor;
                var start = tb.SelectionStart;
                var length = tb.SelectionLength;
                var text = tb.Text;
                var sel = length > 0 ? text.Substring(start, length) : string.Empty;
                if (string.IsNullOrEmpty(sel))
                {
                    PrefixSelection(prefix);
                    return;
                }
                var lines = sel.Replace("\r", string.Empty).Split('\n');
                var transformed = string.Join("\n", lines.Select(l => string.IsNullOrWhiteSpace(l) ? l : prefix + l));
                tb.SelectedText = transformed;
            }
            catch { }
        }

        private class InputItem
        {
            public string Name { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string Type { get; set; } = "string"; // string|number|bool
            public string? Default { get; set; }
            public bool Required { get; set; }
        }
    }
}
