using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Markdig;
using Pw.Hub.Services;

namespace Pw.Hub.Windows
{
    public partial class ModulesApiEditorWindow : Window
    {
        private readonly ModuleDto _existing;
        private readonly ObservableCollection<InputItem> _inputs = new();
        private CancellationTokenSource _previewCts;

        public ModulesApiEditorWindow(ModuleDto existing = null)
        {
            InitializeComponent();
            _existing = existing;

            // Bind grid
            InputsGrid.ItemsSource = _inputs;

            Loaded += async (_, _) =>
            {
                await InitPreviewAsync();
                // Ensure clipping is updated after window is fully loaded
                await Task.Delay(200);
                UpdateWebViewClipping();
            };

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
                        Default = i.Default,
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
                
                // Update clipping on size change
                PreviewWebView.SizeChanged += (s, e) => UpdateWebViewClipping();
                PreviewContainer.SizeChanged += (s, e) => UpdateWebViewClipping();
                this.SizeChanged += (s, e) => UpdateWebViewClipping();
            }
            catch { }
            await UpdatePreviewAsync();
            UpdatePreviewVisibility();
            
            // Initial clipping update
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateWebViewClipping();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public CreateOrUpdateModule GetRequest()
        {
            var inputs = _inputs
                .Select(i => new InputDefinitionDto
                {
                    Name = i.Name?.Trim() ?? string.Empty,
                    Label = string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label,
                    Type = string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type,
                    Default = i.Default,
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
            // Commit any pending edits in the grid so bindings (e.g., Type) are pushed to the underlying object
            try
            {
                InputsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                InputsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch { }

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
            {
                DebouncePreview();
                // Update clipping when preview is shown
                Dispatcher.BeginInvoke(new Action(() => UpdateWebViewClipping()), 
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
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

        private void MainScrollViewer_OnScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            // Update WebView2 clipping when scrolling
            UpdateWebViewClipping();
        }

        private void UpdateWebViewClipping()
        {
            try
            {
                if (PreviewWebView?.CoreWebView2 == null || MainScrollViewer == null || PreviewContainer == null || PreviewHeader == null)
                    return;

                // Get the position of PreviewContainer relative to ScrollViewer content
                var relativePos = PreviewContainer.TransformToAncestor(MainScrollViewer).Transform(new System.Windows.Point(0, 0));
                
                var scrollViewerHeight = MainScrollViewer.ActualHeight;
                var containerHeight = PreviewContainer.ActualHeight;
                var headerHeight = PreviewHeader.ActualHeight;
                
                // Calculate how much the container is clipped at top and bottom
                // If relativePos.Y is negative, container top is above viewport (scrolled up)
                var containerClipTop = Math.Max(0, -relativePos.Y);
                
                // WebView2 should be clipped less because header takes some space
                // Only clip WebView if the clipping goes beyond the header
                var webViewClipTop = Math.Max(0, containerClipTop - headerHeight);
                
                // If container bottom is below viewport, clip from bottom
                var containerBottom = relativePos.Y + containerHeight;
                var clipBottom = Math.Max(0, containerBottom - scrollViewerHeight);
                
                // Apply visual clipping to container
                if (containerClipTop > 0 || clipBottom > 0)
                {
                    var visibleHeight = Math.Max(0, containerHeight - containerClipTop - clipBottom);
                    var clipRect = new System.Windows.Media.RectangleGeometry(
                        new System.Windows.Rect(0, containerClipTop, PreviewContainer.ActualWidth, visibleHeight));
                    PreviewContainer.Clip = clipRect;
                    
                    // Set margin on WebView2 accounting for header height
                    PreviewWebView.Margin = new System.Windows.Thickness(0, webViewClipTop, 0, clipBottom);
                }
                else
                {
                    PreviewContainer.Clip = null;
                    PreviewWebView.Margin = new System.Windows.Thickness(0);
                }
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
                var css = @"html{height:100%;margin:0;padding:0;overflow:auto;}
                    body{margin:0;padding:8px 12px 12px 12px;font-family:Segoe UI,Arial,sans-serif;background:#171A21;color:#C7D5E0;box-sizing:border-box;}
                    *{box-sizing:border-box;max-width:100%;}
                    h1,h2,h3,h4,h5,h6{color:#C7D5E0;margin:0.5em 0;}
                    a{color:#66C0F4}
                    pre{background:#1B2838;padding:8px;border-radius:6px;overflow-x:auto;white-space:pre-wrap;word-wrap:break-word;border:1px solid #2A475E;margin:8px 0;}
                    code{background:#1B2838;padding:2px 4px;border-radius:4px;border:1px solid #2A475E;word-break:break-word;}
                    blockquote{border-left:3px solid #2A475E;margin:8px 0;padding:4px 12px;color:#B8C6D1}
                    table{border-collapse:collapse;width:100%;margin:8px 0;}
                    th,td{border:1px solid #2A475E;padding:6px;text-align:left;}
                    ul,ol{padding-left:22px;margin:8px 0;}
                    p{margin:0.5em 0;}";
                var doc = $"<!DOCTYPE html><html><head><meta charset='utf-8'><style>{css}</style></head><body>{html}</body></html>";
                PreviewWebView.NavigateToString(doc);
                
                // Update clipping after content loads
                await Task.Delay(100); // Small delay to let content render
                UpdateWebViewClipping();
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

        private class InputItem : INotifyPropertyChanged
        {
            public string Name
            {
                get;
                set => SetField(ref field, value);
            } = string.Empty;

            public string Label
            {
                get;
                set => SetField(ref field, value);
            } = string.Empty;

            public string Type
            {
                get;
                set => SetField(ref field, value);
            } = "string"; // string|number|bool|password

            public string Default
            {
                get;
                set => SetField(ref field, value);
            }

            public bool Required
            {
                get;
                set => SetField(ref field, value);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }
    }
}