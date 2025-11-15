using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Markdig;
using Pw.Hub.Services;
using System.Linq;

namespace Pw.Hub.Windows
{
    public partial class ModulesApiEditorWindow : Window
    {
        public bool IsSaved { get; private set; }
        private readonly ModuleDto _existing;
        private CancellationTokenSource _previewCts;
        private string _script = string.Empty;
        // ViewModel для MVVM
        private readonly ViewModels.ModulesApiEditorViewModel _vm = new();

        public ModulesApiEditorWindow(ModuleDto existing = null)
        {
            InitializeComponent();
            _existing = existing;
            // MVVM: назначаем VM как DataContext (XAML пока использует прямые элементы для минимальных правок)
            DataContext = _vm;

            // Bind grid: теперь используется MVVM-привязка в XAML к Vm.Inputs
            // InputsGrid.ItemsSource = _inputs; // не требуется

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
                _script = existing.Script ?? string.Empty;

                // Инициализируем VM из существующих данных
                _vm.Name = NameText.Text;
                _vm.Version = VersionText.Text;
                _vm.Description = DescriptionEditor.Text;
                _vm.Script = _script;
                _vm.Inputs.Clear();
                foreach (var i in existing.Inputs ?? Array.Empty<InputDefinitionDto>())
                {
                    _vm.Inputs.Add(new ViewModels.ModulesApiEditorViewModel.InputItem
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
            else
            {
                _script = string.Empty;
                // Начальные значения VM
                _vm.Name = string.Empty;
                _vm.Version = "1.0.0";
                _vm.Description = string.Empty;
                _vm.Script = _script;
            }

            // Подписка на закрытие из VM
            _vm.RequestClose += result => 
            { 
                if (result == true) IsSaved = true;
                try { DialogResult = result; } catch { } 
                Close(); 
            };
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
            catch
            {
            }

            await UpdatePreviewAsync();
            UpdatePreviewVisibility();

            // Initial clipping update
            Dispatcher.BeginInvoke(new Action(() => { UpdateWebViewClipping(); }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public CreateOrUpdateModule GetRequest()
        {
            // MVVM: данные уже находятся в VM благодаря привязкам — формируем запрос напрямую
            return _vm.BuildRequest();
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

            // Обновляем _script из VM перед закрытием (синхронизация с Lua редактором)
            try { _script = _vm.Script ?? string.Empty; } catch { }

            if (string.IsNullOrWhiteSpace(_vm.Name) || string.IsNullOrWhiteSpace(_vm.Script))
            {
                MessageBox.Show(this, "Имя и скрипт обязательны");
                return;
            }

            // Помечаем как сохранённый для немодального сценария
            IsSaved = true;
            // Сообщаем VM (для внешних подписчиков)
            try { _vm.GetType().GetProperty("IsSaved")?.SetValue(_vm, true); } catch { }
            // Поддержка модального сценария (если окно открыто через ShowDialog)
            try { DialogResult = true; } catch { }
            // Закрываем окно
            try { Close(); } catch { }
        }

        private void AddInput_Click(object sender, RoutedEventArgs e)
        {
            _vm.Inputs.Add(new ViewModels.ModulesApiEditorViewModel.InputItem
                { Name = "param", Label = "Параметр", Type = "string", Default = string.Empty, Required = false });
        }

        private async void OpenLuaEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Application.Current.MainWindow is not Pw.Hub.MainWindow mainWindow) return;
                var runner = mainWindow.AccountPage?.LuaRunner;
                if (runner == null)
                {
                    MessageBox.Show(this, "Lua Runner недоступен", "Lua", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Гарантируем, что код скрипта загружен (если пришли из списка, где Script пуст)
                await EnsureFullModuleLoadedAsync();
                var initialCode = !string.IsNullOrWhiteSpace(_vm.Script) ? _vm.Script : (_script ?? string.Empty);

                // Не закрываем окно создания/редактирования модуля.
                // Открываем LuaEditor немодально, чтобы не блокировать MainWindow и оставить это окно открытым.
                var editor = new LuaEditorWindow(runner) { Owner = Application.Current.MainWindow };
                editor.SetCode(initialCode);
                // Pass current inputs to request arguments on run/debug
                var apiInputs = _vm.Inputs.Select(i => new InputDefinitionDto
                {
                    Name = i.Name ?? string.Empty,
                    Label = string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label,
                    Type = string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type,
                    Default = i.Default,
                    Required = i.Required
                }).ToList();
                editor.ApiInputs = apiInputs;

                // Open editor modeless so MainWindow stays responsive for Lua API operations
                editor.Closed += (o, args2) =>
                {
                    try
                    {
                        _script = editor.GetCode();
                        _vm.Script = _script;
                        // Bring module editor back to front after closing Lua editor
                        try { this.Activate(); } catch { }
                    }
                    catch { }
                };
                editor.Show();
            }
            catch
            {
            }
        }

        private async Task EnsureFullModuleLoadedAsync()
        {
            // Для редактирования существующего модуля данные, полученные из поиска, могут не содержать Script/Inputs.
            // Дозагружаем полный модуль по Id перед открытием Lua-редактора/сохранением.
            try
            {
                if (_existing == null)
                    return;

                var needScript = string.IsNullOrWhiteSpace(_script);
                var needInputs = _vm.Inputs == null || _vm.Inputs.Count == 0;
                if (!needScript && !needInputs)
                    return; // уже всё есть

                var api = new ModulesApiClient();
                var full = await api.GetModuleAsync(_existing.Id);
                if (full != null)
                {
                    if (needScript)
                    {
                        _script = full.Script ?? string.Empty;
                        _vm.Script = _script;
                    }

                    if (needInputs)
                    {
                        _vm.Inputs.Clear();
                        foreach (var i in full.Inputs ?? Array.Empty<InputDefinitionDto>())
                        {
                            _vm.Inputs.Add(new ViewModels.ModulesApiEditorViewModel.InputItem
                            {
                                Name = i.Name ?? string.Empty,
                                Label = string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label,
                                Type = string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type,
                                Default = i.Default,
                                Required = i.Required
                            });
                        }
                    }
                }

                // Локальный фоллбэк: если API не вернул скрипт, пробуем прочитать из локальной установки
                if (needScript && string.IsNullOrWhiteSpace(_script))
                {
                    try
                    {
                        var svc = new ModuleService();
                        var locals = svc.LoadModules();
                        var def = locals?.FirstOrDefault(x => string.Equals(x.Id, _existing.Id.ToString(), StringComparison.OrdinalIgnoreCase));
                        if (def != null)
                        {
                            var baseDir = AppContext.BaseDirectory;
                            var p1 = System.IO.Path.Combine(baseDir, def.Script ?? string.Empty);
                            var p2 = System.IO.Path.Combine(baseDir, "Scripts", def.Script ?? string.Empty);
                            string code = null;
                            if (System.IO.File.Exists(p1)) code = System.IO.File.ReadAllText(p1);
                            else if (System.IO.File.Exists(p2)) code = System.IO.File.ReadAllText(p2);
                            if (!string.IsNullOrEmpty(code))
                            {
                                _script = code;
                                _vm.Script = _script;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                // Молча игнорируем ошибки дозагрузки — откроем редактор с тем, что есть
            }
        }

        private void RemoveInput_Click(object sender, RoutedEventArgs e)
        {
            if (InputsGrid.SelectedItem is ViewModels.ModulesApiEditorViewModel.InputItem item)
            {
                _vm.Inputs.Remove(item);
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
            catch
            {
            }
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
                if (PreviewWebView?.CoreWebView2 == null || MainScrollViewer == null || PreviewContainer == null ||
                    PreviewHeader == null)
                    return;

                // Get the position of PreviewContainer relative to ScrollViewer content
                var relativePos = PreviewContainer.TransformToAncestor(MainScrollViewer)
                    .Transform(new System.Windows.Point(0, 0));

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
            catch
            {
            }
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
                catch
                {
                }
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
                var doc =
                    $"<!DOCTYPE html><html><head><meta charset='utf-8'><style>{css}</style></head><body>{html}</body></html>";
                PreviewWebView.NavigateToString(doc);

                // Update clipping after content loads
                await Task.Delay(100); // Small delay to let content render
                UpdateWebViewClipping();
            }
            catch
            {
            }
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
            catch
            {
            }
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
            catch
            {
            }
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
            catch
            {
            }
        }

        private class InputItem : INotifyPropertyChanged
        {
            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => SetField(ref _name, value);
            }

            private string _label = string.Empty;
            public string Label
            {
                get => _label;
                set => SetField(ref _label, value);
            }

            private string _type = "string"; // string|number|bool|password
            public string Type
            {
                get => _type;
                set => SetField(ref _type, value);
            }

            private string _default;
            public string Default
            {
                get => _default;
                set => SetField(ref _default, value);
            }

            private bool _required;
            public bool Required
            {
                get => _required;
                set => SetField(ref _required, value);
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

        private class AIConfig
        {
            public string OllamaApiKey { get; set; }
        }
    }
}