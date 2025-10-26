using System.IO;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using Pw.Hub.Tools;
using System.Xml;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Pw.Hub.Windows;

public partial class LuaEditorWindow : Window
{
    // ==== AI Chat integration fields ====
    private readonly HttpClient _aiHttp = new HttpClient() { Timeout = TimeSpan.FromSeconds(120) };
    private const string OllamaCloudUrl = "https://ollama.com/api/chat";
    private string _aiApiKey = "your-ollama-cloud-api-key";
    private readonly List<AiMessage> _aiMessages = new();
    private string _aiLastCode; // last extracted code block from AI
    private string _aiLastDiff; // last diff preview
    private Border _aiTypingBubble; // temporary typing indicator bubble

    private sealed class AiMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    private sealed class AiChatRequest
    {
        public string model { get; set; }
        public List<AiMessage> messages { get; set; }
        public bool stream { get; set; } = false;
    }

    private sealed class AiChatResponse
    {
        public List<AiMessage> messages { get; set; }
    }

    private readonly LuaScriptRunner _runner;
    private CompletionWindow _completionWindow;
    private readonly HashSet<int> _breakpoints = new();
    private BreakpointBackgroundRenderer _bpRenderer;
    private bool _isRunning;
    private bool _isDebugging;
    private bool _isSplitterDragging;
    private double _aiPaneLastWidth = 460; // remember last visible width for AI pane

    // Simple type model for Lua API objects (for autocomplete after '.')
    private sealed class ObjectType
    {
        public string Name { get; }
        public string[] Fields { get; }

        public ObjectType(string name, params string[] fields)
        {
            Name = name;
            Fields = fields ?? Array.Empty<string>();
        }
    }

    // Known object types
    private static readonly ObjectType AccountType = new(
        "Account",
        // Fields based on Pw.Hub.Models.Account
        "Id", "Name", "Email", "ImageSource", "LastVisit", "Servers", "ImageUri", "SquadId", "Squad"
    );

    private static readonly ObjectType AccountServerType = new(
        "AccountServer",
        // Fields based on Pw.Hub.Models.AccountServer
        "Id", "OptionId", "Name", "DefaultCharacterOptionId", "Characters", "CharactersWithPlaceholder", "AccountId"
    );

    private static readonly ObjectType CharacterType = new(
        "AccountCharacter",
        // Fields based on Pw.Hub.Models.AccountCharacter
        "Id", "OptionId", "Name", "Server", "ServerId"
    );

    private static readonly ObjectType SquadType = new(
        "Squad",
        // Fields based on Pw.Hub.Models.Squad
        "Id", "Name", "Accounts"
    );

    // Map of common callback parameter names to their types (as inserted by our snippets)
    private static readonly Dictionary<string, ObjectType> ParamTypeByName = new(StringComparer.Ordinal)
    {
        { "acc", AccountType },
        { "Account", AccountType },
        { "account", AccountType },
        // collections and other callback args
        { "accounts", AccountType }, // treat as element type for convenience
        { "Servers", AccountServerType },
        { "servers", AccountServerType },
        { "Characters", CharacterType },
        { "characters", CharacterType },
        { "Squad", SquadType },
        { "squad", SquadType },
        { "result", null },
        { "exists", null }, { "found", null }, { "isAuth", null }, { "ok", null }
    };

    // Autocomplete metadata and cache
    private readonly ApiSymbol[] _apiSymbols = new[]
    {
        // Account (callback-based)
        new ApiSymbol("Account_GetAccountCb", @"Account_GetAccountCb(accountId, function(acc)
    __CURSOR__
end)", "Получить аккаунт по id и вернуть в callback(acc)"),
        new ApiSymbol("Account_IsAuthorizedCb", @"Account_IsAuthorizedCb(accountId, function(isAuth)
    __CURSOR__
end)", "Проверить авторизацию аккаунта"),
        new ApiSymbol("Account_GetAccountsJsonCb", @"Account_GetAccountsJsonCb(function(json)
    __CURSOR__
end)", "Список аккаунтов (JSON)"),
        new ApiSymbol("Account_GetAccountsCb", @"Account_GetAccountsCb(function(accounts)
    __CURSOR__
end)", "Список аккаунтов (таблица)"),
        new ApiSymbol("Account_ChangeAccountCb", @"Account_ChangeAccountCb(accountId, function(ok)
    __CURSOR__
end)", "Сменить активный аккаунт"),

        // Browser (callback-based)
        new ApiSymbol("Browser_NavigateCb", @"Browser_NavigateCb(url, function()
    __CURSOR__
end)", "Открыть url"),
        new ApiSymbol("Browser_ReloadCb", @"Browser_ReloadCb(function()
    __CURSOR__
end)", "Перезагрузить страницу"),
        new ApiSymbol("Browser_ExecuteScriptCb", @"Browser_ExecuteScriptCb(jsCode, function(result)
    __CURSOR__
end)", "Выполнить JS и вернуть результат"),
        new ApiSymbol("Browser_ElementExistsCb", @"Browser_ElementExistsCb(selector, function(exists)
    __CURSOR__
end)", "Проверить наличие элемента"),
        new ApiSymbol("Browser_WaitForElementCb", @"Browser_WaitForElementCb(selector, timeoutMs, function(found)
    __CURSOR__
end)", "Ждать появления элемента"),

        // Helpers
        new ApiSymbol("Print", "Print(value)", "Вывести текст в консоль"),
        new ApiSymbol("DelayCb", @"DelayCb(ms, function()
    __CURSOR__
end)", "Задержка с колбэком"),
        new ApiSymbol("ReportProgress", "ReportProgress(percent)", "Обновить прогресс"),
        new ApiSymbol("ReportProgressMsg", "ReportProgressMsg(percent, message)", "Обновить прогресс с сообщением"),
        new ApiSymbol("Complete", "Complete()", "Завершить задачу/скрипт")
    };

    private List<ICompletionData> _cachedCompletionItems;

    public LuaEditorWindow(LuaScriptRunner runner)
    {
        _runner = runner;
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        InitAiConfig();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize AI brave UI state
        try { OnAiBraveToggled(AiBraveCheck, new RoutedEventArgs()); } catch { }

        // Route Lua Print to our output box
        _runner.SetPrintSink(AppendLog);

        // Syntax highlighting (robust loading from pack URI or file system)
        try
        {
            // 1) Try load from WPF Resource via pack URI
            var packUri = new Uri("pack://application:,,,/Assets/LuaEditor/lua.xshd", UriKind.Absolute);
            var res = Application.GetResourceStream(packUri);
            if (res != null)
            {
                using var reader = new XmlTextReader(res.Stream);
                Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            else
            {
                // 2) Fallback: load from output folder (CopyToOutputDirectory)
                var baseDir = AppContext.BaseDirectory;
                var xshdPath = Path.Combine(baseDir, "Assets", "LuaEditor", "lua.xshd");
                if (File.Exists(xshdPath))
                {
                    using var s = File.OpenRead(xshdPath);
                    using var reader = new XmlTextReader(s);
                    Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
                else
                {
                    AppendLog("Не удалось найти lua.xshd ни в ресурсах, ни в папке вывода.");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Ошибка загрузки подсветки: {ex.Message}");
        }

        // Editor template if empty
        if (Editor != null && string.IsNullOrWhiteSpace(Editor.Text))
        {
            Editor.Text = "-- Lua script\n-- Use Account_*Cb and Browser_*Cb APIs\nPrint('Hello from editor')\n";
        }

        // Autocomplete hooks
        Editor.TextArea.TextEntering += TextAreaOnTextEntering;
        Editor.TextArea.TextEntered += TextAreaOnTextEntered;
        Editor.PreviewKeyDown += EditorOnPreviewKeyDown;

        // Register breakpoint renderer for visual markers
        try
        {
            _bpRenderer = new BreakpointBackgroundRenderer(_breakpoints);
            Editor.TextArea.TextView.BackgroundRenderers.Add(_bpRenderer);
            Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }
        catch
        {
        }

        UpdateBpCount();
    }

    private void OnClosed(object sender, EventArgs e)
    {
        // Stop routing Print to this window
        _runner.SetPrintSink(null);
        // Dispose current Lua VM used by editor so pending callbacks are cancelled and resources released
        try
        {
            _runner.Stop();
        }
        catch
        {
        }

        SetRunState(false, false);
    }

    private void EditorOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ShowCompletion();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F9)
        {
            ToggleBreakpointAtCaret();
            e.Handled = true;
            return;
        }
    }

    private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
    {
        // Автодополнение автоматически открываем только после точки
        if (e.Text == ".")
        {
            ShowCompletion();
        }
    }

    private void TextAreaOnTextEntering(object sender, TextCompositionEventArgs e)
    {
        if (_completionWindow != null && e.Text.Length > 0)
        {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private void ShowCompletion()
    {
        try
        {
            if (_completionWindow != null && _completionWindow.IsVisible)
                return; // already open; let built-in filtering handle it

            // Detect if this is a member access (identifier.) and infer type
            var memberItems = TryBuildMemberCompletionItems();

            // Build and cache global API completion items once
            if (_cachedCompletionItems == null)
            {
                _cachedCompletionItems = new List<ICompletionData>(_apiSymbols.Length);
                foreach (var s in _apiSymbols)
                {
                    _cachedCompletionItems.Add(new SignatureCompletionData(s));
                }
            }

            _completionWindow = new CompletionWindow(Editor.TextArea)
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                Owner = this
            };

            // Apply app styles/brushes/fonts
            try
            {
                var bg = TryFindResource("BackgroundSecondaryBrush") as Brush ?? Brushes.White;
                var fg = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
                var br = TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;
                var mono = TryFindResource("MonospaceFont") as FontFamily ?? new FontFamily("Consolas");

                _completionWindow.Background = bg;
                _completionWindow.BorderBrush = br;

                var list = _completionWindow.CompletionList;
                list.Background = bg;
                list.Foreground = fg;
                list.BorderBrush = br;
                list.FontFamily = mono;
                list.FontSize = Editor.FontSize; // match editor size
            }
            catch
            {
            }

            var data = _completionWindow.CompletionList.CompletionData;
            data.Clear();

            var source = (memberItems != null && memberItems.Count > 0) ? memberItems : _cachedCompletionItems;
            foreach (var item in source)
                data.Add(item);

            _completionWindow.Closed += (_, _) => _completionWindow = null;
            _completionWindow.Show();
        }
        catch
        {
        }
    }

    private List<ICompletionData> TryBuildMemberCompletionItems()
    {
        try
        {
            if (Editor?.TextArea == null) return null;
            var caret = Editor.TextArea.Caret;
            var offset = Math.Max(0, Math.Min(Editor.Document.TextLength, caret.Offset));
            if (offset == 0) return null;

            // We expect a '.' just typed at position offset-1
            if (Editor.Document.GetCharAt(Math.Max(0, offset - 1)) != '.')
                return null;

            int dotPos = offset - 1;
            var text = Editor.Document.Text;

            // Try case 1: identifier before the dot (e.g., acc.)
            int idEnd = dotPos;
            int idStart = idEnd;
            while (idStart > 0)
            {
                var ch = text[idStart - 1];
                if (char.IsLetterOrDigit(ch) || ch == '_') idStart--;
                else break;
            }

            if (idStart < idEnd)
            {
                var ident = text.Substring(idStart, idEnd - idStart);
                if (!string.IsNullOrWhiteSpace(ident))
                {
                    // 1) Fast path: default snippet parameter names mapping
                    if (ParamTypeByName.TryGetValue(ident, out var otype1) && otype1 != null)
                    {
                        return BuildFieldItemsForType(otype1);
                    }

                    // 2) Heuristic: try to detect callback param name from call like SomeApi(..., function(param)
                    // Scan a window of text before caret for pattern: function(<ident>) within an API call
                    var windowStart1 = Math.Max(0, idStart - 200);
                    var window1 = text.Substring(windowStart1, idStart - windowStart1);
                    // Match last occurrence of function(<name>)
                    var m1 = Regex.Match(window1, @"function\s*\(\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\)\s*$",
                        RegexOptions.RightToLeft);
                    if (m1.Success)
                    {
                        var paramName1 = m1.Groups[1].Value;
                        if (string.Equals(paramName1, ident, StringComparison.Ordinal))
                        {
                            // Try to see preceding API name in the same window
                            var apiMatch1 = Regex.Match(window1,
                                @"([A-Za-z_][A-Za-z0-9_]*)\s*\(.*function\s*\(\s*" + Regex.Escape(paramName1) +
                                @"\s*\)", RegexOptions.Singleline);
                            if (apiMatch1.Success)
                            {
                                var apiName1 = apiMatch1.Groups[1].Value;
                                var inferred1 = InferTypeFromApiCallback(apiName1);
                                if (inferred1 != null)
                                    return BuildFieldItemsForType(inferred1);
                            }
                        }
                    }
                }
            }

            // Try case 2: bracket indexing before the dot (e.g., accounts[i]. or accounts[1].)
            // Look for ']' immediately before the dot or any non-space chars between ']' and '.'
            int p = dotPos - 1;
            while (p >= 0 && char.IsWhiteSpace(text[p])) p--;
            if (p >= 0 && text[p] == ']')
            {
                int closeBracket = p;
                // Find matching '[' scanning left (no nested brackets expected in simple index expressions)
                int q = closeBracket - 1;
                while (q >= 0 && text[q] != '[')
                {
                    // Stop if newline encountered — unlikely part of the same expression
                    if (text[q] == '\n' || text[q] == '\r') break;
                    q--;
                }

                if (q >= 1 && text[q] == '[')
                {
                    // Now parse identifier right before '['
                    int idEnd2 = q;
                    int idStart2 = idEnd2;
                    while (idStart2 > 0)
                    {
                        var ch2 = text[idStart2 - 1];
                        if (char.IsLetterOrDigit(ch2) || ch2 == '_') idStart2--;
                        else break;
                    }

                    if (idStart2 < idEnd2)
                    {
                        var ident2 = text.Substring(idStart2, idEnd2 - idStart2);
                        if (!string.IsNullOrWhiteSpace(ident2))
                        {
                            if (ParamTypeByName.TryGetValue(ident2, out var otype2) && otype2 != null)
                            {
                                return BuildFieldItemsForType(otype2);
                            }
                        }
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static ObjectType InferTypeFromApiCallback(string apiName)
    {
        // Map known API names to their callback parameter object types
        switch (apiName)
        {
            case "Account_GetAccountCb": return AccountType;
            default: return null;
        }
    }

    private static List<ICompletionData> BuildFieldItemsForType(ObjectType type)
    {
        var list = new List<ICompletionData>(type.Fields.Length);
        foreach (var f in type.Fields)
        {
            list.Add(new FieldCompletionData(f, type.Name));
        }

        return list;
    }

    private void AppendLog(string text)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(AppendLog), text);
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
            if (OutputBox.Text.Length == 0)
                OutputBox.Text = line;
            else
                OutputBox.AppendText("\r\n" + line);

            // Ensure scroll happens after layout pass to reliably keep view pinned to bottom
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    OutputBox.CaretIndex = OutputBox.Text?.Length ?? 0;
                    OutputBox.UpdateLayout();
                    OutputBox.ScrollToEnd();
                    try
                    {
                        OutputScroll?.ScrollToBottom();
                    }
                    catch
                    {
                    }
                }
                catch
                {
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch
        {
        }
    }

    private void SetRunState(bool running, bool debugging)
    {
        _isRunning = running;
        _isDebugging = debugging;
        try
        {
            if (RunBtn != null) RunBtn.IsEnabled = !running && !debugging;
            if (DebugBtn != null) DebugBtn.IsEnabled = !running && !debugging;
            if (StopBtn != null) StopBtn.IsEnabled = running || debugging;
            if (Editor != null) Editor.IsReadOnly = running || debugging;
        }
        catch
        {
        }
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
        SetRunState(true, false);
        try
        {
            // Очистить вывод перед новым запуском
            OutputBox.Clear();
            AppendLog("Запуск скрипта...");
            var code = Editor?.Text ?? string.Empty;
            await _runner.RunCodeAsync(code);
            AppendLog("Выполнение скрипта завершено!");
        }
        catch (Exception ex)
        {
            AppendLog($"Произошла ошибка при выполнения скрипта: {ex.Message}");
        }
        finally
        {
            SetRunState(false, false);
        }
    }

    private async void OnDebugClick(object sender, RoutedEventArgs e)
    {
        SetRunState(false, true);
        try
        {
            // Очистить вывод перед началом отладки
            OutputBox.Clear();
            AppendLog("Запуск отладки скрипта...");
            var code = Editor?.Text ?? string.Empty;
            var bps = _breakpoints.ToArray();
            await _runner.RunCodeWithBreakpointsAsync(code, bps, OnDebugBreak);
            AppendLog("Отладка скрипта завершена!");
        }
        catch (Exception ex)
        {
            AppendLog($"Произошла ошибка при отладке скрипта: {ex.Message}");
        }
        finally
        {
            SetRunState(false, false);
        }
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _runner.Stop();
            AppendLog("[Остановлено пользователем]");
        }
        catch
        {
        }
        finally
        {
            SetRunState(false, false);
        }
    }

    private bool OnDebugBreak(int line, IDictionary<string, object> locals, IDictionary<string, object> globals)
    {
        try
        {
            // Show a dedicated variables window instead of MessageBox for better inspection
            Dispatcher.Invoke(() =>
            {
                var dlg = new DebugVariablesWindow { Owner = this };
                dlg.SetData(line, locals, globals);
                dlg.ShowDialog();
            });
        }
        catch
        {
        }

        return true;
    }

    private static string FormatVars(IDictionary<string, object> vars)
    {
        if (vars == null || vars.Count == 0) return "<пусто>";
        var sb = new System.Text.StringBuilder();
        foreach (var kv in vars)
        {
            sb.Append(kv.Key);
            sb.Append(" = ");
            sb.AppendLine(ToDisplayString(kv.Value));
        }

        return sb.ToString();
    }

    private static string ToDisplayString(object value)
    {
        try
        {
            if (value == null) return "nil";
            if (value is string s) return '"' + s + '"';
            if (value is bool b) return b ? "true" : "false";
            if (value is IDictionary<string, object> dict)
            {
                var inner = string.Join(", ", dict.Select(p => p.Key + ":" + ToDisplayString(p.Value)));
                return "{" + inner + "}";
            }

            return value.ToString();
        }
        catch
        {
            return value?.ToString() ?? "nil";
        }
    }

    private void ToggleBreakpointAtCaret()
    {
        try
        {
            var line = Editor.TextArea.Caret.Line; // 1-based
            if (_breakpoints.Contains(line))
            {
                _breakpoints.Remove(line);
            }
            else
            {
                _breakpoints.Add(line);
            }

            UpdateBpCount();
            InvalidateBreakpointMarks();
        }
        catch
        {
        }
    }

    private void InvalidateBreakpointMarks()
    {
        try
        {
            Editor?.TextArea?.TextView?.InvalidateLayer(KnownLayer.Background);
        }
        catch
        {
        }
    }

    private void UpdateBpCount()
    {
        try
        {
            if (BpCountText != null)
                BpCountText.Text = $"({_breakpoints.Count} точек)";
        }
        catch
        {
        }
    }

    private void OnClearOutputClick(object sender, RoutedEventArgs e)
    {
        OutputBox.Clear();
    }

    private void OutputBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action<object, System.Windows.Controls.TextChangedEventArgs>(OutputBox_OnTextChanged), sender,
                    e);
                return;
            }

            // Schedule scroll after layout to ensure pinned to bottom even with outer ScrollViewer
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    OutputBox.CaretIndex = OutputBox.Text?.Length ?? 0;
                    OutputBox.UpdateLayout();
                    OutputBox.ScrollToEnd();
                    try
                    {
                        OutputScroll?.ScrollToBottom();
                    }
                    catch
                    {
                    }
                }
                catch
                {
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch
        {
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ===== AI CHAT INTEGRATION =====

    private void InitAiConfig()
    {
        _aiApiKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY") ?? _aiApiKey;

        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config", "ai_settings.json");
            if (File.Exists(configPath))
            {
                var config = JsonSerializer.Deserialize<AIConfig>(File.ReadAllText(configPath));
                _aiApiKey = config?.OllamaApiKey ?? _aiApiKey;
            }
            else
            {
                var settingsWindow = new ApiKeySettingsWindow(_aiApiKey);
                if (settingsWindow.ShowDialog() == true)
                {
                    _aiApiKey = settingsWindow.ApiKey;
                }
            }
        }
        catch
        {
            // Игнорируем ошибки чтения конфига
        }
    }

    private void OnAiToggleClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Don't allow toggling the pane while splitter is in drag operation
            if (_isSplitterDragging)
                return;

            // Toggle AI pane without setting zero width to avoid GridSplitter nulls
            if (AiPaneRoot.Visibility != Visibility.Visible)
            {
                // Restore last width or default, enforce minimal width 600
                if (AiPaneColumn.ActualWidth > 10)
                    _aiPaneLastWidth = AiPaneColumn.ActualWidth;
                AiPaneColumn.MinWidth = 600;
                AiPaneColumn.Width = new GridLength(Math.Max(600, _aiPaneLastWidth));
                AiPaneRoot.Visibility = Visibility.Visible;
            }
            else
            {
                // Remember current width before hiding
                if (AiPaneColumn.ActualWidth > 10)
                    _aiPaneLastWidth = AiPaneColumn.ActualWidth;
                AiPaneRoot.Visibility = Visibility.Collapsed;
                // Allow full collapse when hidden
                AiPaneColumn.MinWidth = 0;
                AiPaneColumn.Width = new GridLength(0);
            }
        }
        catch
        {
        }
    }

    private void OnAiNewSessionClick(object sender, RoutedEventArgs e)
    {
        _aiMessages.Clear();
        _aiLastCode = null;
        _aiLastDiff = null;
        ClearDiff();
        AiApplyBtn.IsEnabled = false;
        AiMessagesPanel.Children.Clear();
        AppendAiBubble("system", "Новая сессия начата. Опишите задачу для AI.");
    }

    private void ClearDiff()
    {
        try
        {
            if (AiDiffBox != null)
            {
                AiDiffBox.Document = new FlowDocument(new Paragraph(new Run("")));
            }
        }
        catch
        {
        }
    }

    private async void OnAiSendClick(object sender, RoutedEventArgs e)
    {
        var prompt = (AiInput.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;
        if (string.IsNullOrEmpty(_aiApiKey) || _aiApiKey == "your-ollama-cloud-api-key")
        {
            MessageBox.Show(this,
                "API ключ Ollama Cloud не настроен. Укажите переменную окружения OLLAMA_API_KEY или config/ai_settings.json.",
                "AI", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show pane if hidden
        if (AiPaneRoot.Visibility != Visibility.Visible)
        {
            if (AiPaneColumn.ActualWidth > 10)
                _aiPaneLastWidth = AiPaneColumn.ActualWidth;
            AiPaneColumn.MinWidth = 600;
            AiPaneColumn.Width = new GridLength(Math.Max(600, _aiPaneLastWidth));
            AiPaneRoot.Visibility = Visibility.Visible;
        }

        AppendAiBubble("user", prompt);
        AiInput.Clear();
        AiSendBtn.IsEnabled = false;
        AiApplyBtn.IsEnabled = false;
        ClearDiff();
        ShowTypingIndicator();

        try
        {
            var sysPrompt = BuildAiSystemPrompt();
            var messages = new List<object> { new { role = "system", content = sysPrompt } };
            // include history
            foreach (var m in _aiMessages)
            {
                messages.Add(new { role = m.role, content = m.content });
            }

            // include current editor code as context in the prompt
            var userAugmented = prompt + "\n\nТекущий код редактора ниже между тройными кавычками:\n\"\"\"\n" +
                                (Editor?.Text ?? string.Empty) + "\n\"\"\"";
            messages.Add(new { role = "user", content = userAugmented });

            var req = new
            {
                model = "deepseek-v3.1:671b",
                messages = messages,
                stream = false
            };

            var json = JsonSerializer.Serialize(req,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            _aiHttp.DefaultRequestHeaders.Remove("Authorization");
            _aiHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiApiKey}");
            var resp = await _aiHttp.PostAsync(OllamaCloudUrl, content);
            var respText = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Ошибка API ({(int)resp.StatusCode}): {respText}");
            }

            var assistantText = TryExtractAssistantContent(respText) ?? respText;
            _aiMessages.Add(new AiMessage { role = "user", content = prompt });
            _aiMessages.Add(new AiMessage { role = "assistant", content = assistantText });
            HideTypingIndicator();
            AppendAiBubble("assistant", assistantText);

            // Try extract code block and diff
            var code = ExtractLuaCodeBlock(assistantText);
            if (!string.IsNullOrWhiteSpace(code))
            {
                _aiLastCode = code.Replace("\r\n", "\n");
                var current = (Editor?.Text ?? string.Empty).Replace("\r\n", "\n");
                if (_aiLastCode != current)
                {
                    var diffLines = BuildUnifiedDiffGit(current, _aiLastCode, 3);
                    _aiLastDiff = string.Join("\n", diffLines);
                    RenderDiff(diffLines);

                    // Apply automatically if AI brave is enabled
                    var brave = AiBraveCheck?.IsChecked == true;
                    if (brave)
                    {
                        Editor.Text = _aiLastCode.Replace("\n", Environment.NewLine);
                        AppendAiBubble("system", "AI brave: изменения автоматически применены к редактору.");
                        AiApplyBtn.IsEnabled = false;
                    }
                    else
                    {
                        AiApplyBtn.IsEnabled = true;
                    }
                }
                else
                {
                    RenderDiff(new List<string> { "Изменений нет (код идентичен текущему)." });
                    AiApplyBtn.IsEnabled = false;
                }
            }
            else
            {
                RenderDiff(new List<string> { "AI не вернул код в блоке ```lua ...```. Уточните запрос." });
            }
        }
        catch (Exception ex)
        {
            AppendAiBubble("assistant", "Ошибка: " + ex.Message);
        }
        finally
        {
            AiSendBtn.IsEnabled = true;
        }
    }

    private void OnAiApplyClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_aiLastCode)) return;
        if (MessageBox.Show(this, "Вставить изменения из AI в редактор?", "AI", MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Editor.Text = _aiLastCode.Replace("\n", Environment.NewLine);
            AppendAiBubble("system", "Изменения вставлены в редактор.");
        }
    }

    // Handle splitter drag lifecycle to avoid toggling pane while dragging
    private void OnSplitterDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _isSplitterDragging = true;
        try
        {
            if (AiToggleBtn != null) AiToggleBtn.IsEnabled = false;
        }
        catch
        {
        }
    }

    private void OnSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _isSplitterDragging = false;
        try
        {
            if (AiToggleBtn != null) AiToggleBtn.IsEnabled = true;
        }
        catch
        {
        }
    }

    private void OnAiBraveToggled(object sender, RoutedEventArgs e)
    {
        try
        {
            var brave = AiBraveCheck?.IsChecked == true;
            if (AiApplyBtn != null)
            {
                AiApplyBtn.Visibility = brave ? Visibility.Collapsed : Visibility.Visible;
                AiApplyBtn.IsEnabled = !brave && !string.IsNullOrEmpty(_aiLastDiff) && !string.IsNullOrEmpty(_aiLastCode);
            }

            // If toggled to brave and we already have pending code different from editor — apply immediately
            if (brave && !string.IsNullOrEmpty(_aiLastCode))
            {
                var current = (Editor?.Text ?? string.Empty).Replace("\r\n", "\n");
                var proposed = _aiLastCode.Replace("\r\n", "\n");
                if (!string.Equals(current, proposed, StringComparison.Ordinal))
                {
                    Editor.Text = proposed.Replace("\n", Environment.NewLine);
                    AppendAiBubble("system", "AI brave: изменения автоматически применены к редактору.");
                }
            }
        }
        catch
        {
        }
    }

    private void AppendAiBubble(string role, string text)
    {
        try
        {
            var isUser = string.Equals(role, "user", StringComparison.OrdinalIgnoreCase);
            var isAssistant = string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

            var bubble = new Border
            {
                Background = TryFindResource(isUser ? "BackgroundSecondaryBrush" : "BackgroundTertiaryBrush") as Brush,
                BorderBrush = TryFindResource("BorderBrush") as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 560
            };

            // If assistant returned code, show truncated preview with actions
            string code = null;
            if (isAssistant)
            {
                code = ExtractLuaCodeBlock(text);
            }

            if (!string.IsNullOrEmpty(code))
            {
                var panel = new StackPanel { Orientation = Orientation.Vertical };

                // Title
                var title = new TextBlock
                {
                    Text = "Фрагмент кода (первые 2 строки)",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TryFindResource("TextPrimaryBrush") as Brush
                };
                panel.Children.Add(title);

                // Preview of first two lines
                var lines = code.Replace("\r\n", "\n").Split('\n');
                var preview = string.Join("\n", lines.Take(2));
                if (lines.Length > 2) preview += "\n...";
                var tb = new TextBlock
                {
                    Text = preview,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = TryFindResource("TextPrimaryBrush") as Brush,
                    FontFamily = TryFindResource("MonospaceFont") as FontFamily ?? new FontFamily("Consolas"),
                    Margin = new Thickness(0, 4, 0, 8)
                };
                panel.Children.Add(tb);

                // Actions row
                var actions = new StackPanel
                    { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var copyBtn = new Button
                {
                    Content = "📋 Копировать",
                    Style = TryFindResource("ModernButton") as Style,
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(8, 4, 8, 4),
                    Tag = code
                };
                copyBtn.Click += OnCopyCodeClick;
                var showBtn = new Button
                {
                    Content = "🔎 Показать полностью",
                    Style = TryFindResource("ModernButton") as Style,
                    Padding = new Thickness(8, 4, 8, 4),
                    Tag = code
                };
                showBtn.Click += OnShowFullCodeClick;
                actions.Children.Add(copyBtn);
                actions.Children.Add(showBtn);
                panel.Children.Add(actions);

                bubble.Child = panel;
            }
            else
            {
                var tb = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = TryFindResource("TextPrimaryBrush") as Brush,
                    FontFamily = TryFindResource("MonospaceFont") as FontFamily ?? new FontFamily("Consolas")
                };
                bubble.Child = tb;
            }

            AiMessagesPanel.Children.Add(bubble);
            ScrollChatToBottom();
        }
        catch
        {
        }
    }

    private void OnCopyCodeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button b && b.Tag is string code)
            {
                Clipboard.SetText(code);
                AppendAiBubble("system", "Код скопирован в буфер обмена.");
            }
        }
        catch
        {
        }
    }

    private void OnShowFullCodeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button b && b.Tag is string code)
            {
                ShowCodeDialog(code);
            }
        }
        catch
        {
        }
    }

    private void ScrollChatToBottom()
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ScrollChatToBottom));
                return;
            }

            // Defer to background priority so layout is updated before scrolling
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    AiMessagesScroll?.UpdateLayout();
                    AiMessagesScroll?.ScrollToBottom();
                }
                catch
                {
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch
        {
        }
    }

    private void ShowCodeDialog(string code)
    {
        try
        {
            var win = new Window
            {
                Owner = this,
                Title = "Полный код из ответа AI",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Style = TryFindResource("ModernWindow") as Style
            };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Ответ AI — полный код", 
                Margin = new Thickness(10), 
                FontWeight = FontWeights.SemiBold,
                Style = TryFindResource("ModernTitle") as Style
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var tb = new TextBox
            {
                Text = code?.Replace("\n", Environment.NewLine) ?? string.Empty,
                IsReadOnly = true,
                FontFamily = TryFindResource("MonospaceFont") as FontFamily ?? new FontFamily("Consolas"),
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10),
                Style = TryFindResource("ModernTextBox") as Style
            };
            Grid.SetRow(tb, 1);
            grid.Children.Add(tb);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            var copy = new Button
            {
                Content = "📋 Копировать", Style = TryFindResource("ModernButton") as Style,
                Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4)
            };
            copy.Click += (_, __) =>
            {
                try
                {
                    Clipboard.SetText(code ?? string.Empty);
                }
                catch
                {
                }
            };
            var close = new Button
            {
                Content = "Закрыть", Style = TryFindResource("PrimaryButton") as Style,
                Padding = new Thickness(12, 6, 12, 6)
            };
            close.Click += (_, __) => win.Close();
            footer.Children.Add(copy);
            footer.Children.Add(close);
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            win.Content = grid;
            win.ShowDialog();
        }
        catch
        {
        }
    }

    private string BuildAiSystemPrompt()
    {
        return @"Ты - эксперт по Lua скриптам для автоматизации игровых процессов. 
Сгенерируй чистый, рабочий код на Lua.
Отвечай ТОЛЬКО кодом в одном блоке ```lua ...``` без пояснений.
Если в запросе просят правки, верни весь итоговый файл со внесёнными изменениями. 

ДОСТУПНОЕ API (все функции асинхронные с callback):

=== РАБОТА С АККАУНТАМИ ===
Account_GetAccountCb(cb) - получить текущий аккаунт
Account_GetAccountsCb(cb) - получить все аккаунты (возвращает таблицу с полями: Id, Name, OrderIndex, Squad, Servers)
Account_IsAuthorizedCb(cb) - проверка авторизации (возвращает boolean)
Account_ChangeAccountCb(accountId, cb) - сменить аккаунт

=== РАБОТА С БРАУЗЕРОМ ===
Browser_NavigateCb(url, cb) - перейти по URL
Browser_ReloadCb(cb) - перезагрузить страницу
Browser_ExecuteScriptCb(jsCode, cb) - выполнить JavaScript
Browser_ElementExistsCb(selector, cb) - проверить наличие элемента
Browser_WaitForElementCb(selector, timeoutMs, cb) - ждать появление элемента

=== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ===
Print(text) - вывести текст в лог
DelayCb(ms, cb) - асинхронная задержка
ReportProgress(percent) - отчет о прогрессе
ReportProgressMsg(percent, message) - отчет с сообщением
Complete(result) - завершить выполнение модуля (если скрипт запущен как модуль)
Net_PostJsonCb(url, jsonBody, contentType, cb) - отправить POST запрос (возвращает Success, ResponseBody, Error)

=== СТРУКТУРА ДАННЫХ ===
Аккаунт: {Id, Name, OrderIndex, Squad, Servers[]}
Отряд: {Id, Name, OrderIndex}
Сервер: {Id, Name, OptionId, DefaultCharacterOptionId, Characters[]}
Персонаж: {Id, Name, OptionId}

ВАЖНЫЕ ПРАВИЛА:
1. ВСЕГДА используй асинхронные версии функций (оканчиваются на Cb)
2. Добавляй комментарии на русском языке для основных блоков
3. Обрабатывай возможные ошибки и пограничные случаи
4. Используй понятные именования переменных
5. Логируй ключевые этапы через Print()
6. Если функция Complete доступна - вызывай ее в конце
7. Для работы с таблицами используй ipairs и # для размера
8. Всегда проверяй существование данных перед использованием
9. Функции не могут использовать другие функции, объявленные после них. Поэтому в самом начале объяви все функции для взаимных вызовов, а потом присвой им значение.
10. JavaScript для выполнения должен быть объявлен в однострочной переменной.
11. Выполненный JavaScript может вернуть только строку.
12. Если результатом выполнения нужен массив, то в JavaScript нужно соединить результат в одну строку с разделителем, а в lua потом разбить строку на массив через разделитель.
13. Переходить можно только по ссылкам с доменом pwonline.ru
14. Перед обращением к элементу нужно ждать, пока он появится (1000 МС достаточно).";
    }

    private static string TryExtractAssistantContent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            // shape 1: { message: { content: "..." } }
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
            {
                if (msg.TryGetProperty("content", out var c)) return c.GetString();
            }

            // shape 2: { messages: [ {role:"assistant", content:"..."}, ...] }
            if (doc.RootElement.TryGetProperty("messages", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var role = el.TryGetProperty("role", out var r) ? r.GetString() : null;
                    if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        if (el.TryGetProperty("content", out var c2)) return c2.GetString();
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string ExtractLuaCodeBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Regex.Match(text, "```lua(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value.Trim();
        // fallback: any fenced block
        m = Regex.Match(text, "```(.*?)```", RegexOptions.Singleline);
        if (m.Success) return m.Groups[1].Value.Trim();
        return null;
    }

    private static string BuildUnifiedDiff(string oldText, string newText)
    {
        // Kept for backward compatibility; not used anymore
        var a = (oldText ?? string.Empty).Split('\n');
        var b = (newText ?? string.Empty).Split('\n');
        var sb = new StringBuilder();
        sb.AppendLine("--- Текущий файл");
        sb.AppendLine("+++ Предложение AI");
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                sb.AppendLine("- " + a[i]);
                sb.AppendLine("+ " + b[i]);
            }
        }

        for (int i = n; i < a.Length; i++) sb.AppendLine("- " + a[i]);
        for (int i = n; i < b.Length; i++) sb.AppendLine("+ " + b[i]);
        if (sb.Length == 0) return "Изменений нет";
        return sb.ToString();
    }

    // Build a git-like unified diff with hunks and context
    private static List<string> BuildUnifiedDiffGit(string oldText, string newText, int context = 3)
    {
        var a = (oldText ?? string.Empty).Split('\n');
        var b = (newText ?? string.Empty).Split('\n');
        var lines = new List<string>(a.Length + b.Length + 8)
        {
            "--- Текущий файл",
            "+++ Предложение AI"
        };

        // LCS dynamic programming tables
        int n = a.Length, m = b.Length;
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                if (a[i] == b[j]) dp[i, j] = dp[i + 1, j + 1] + 1;
                else dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        // Build edit script
        var edits = new List<(char tag, string text, int ia, int jb)>();
        int ia = 0, jb = 0;
        while (ia < n && jb < m)
        {
            if (a[ia] == b[jb])
            {
                edits.Add((' ', a[ia], ia, jb));
                ia++;
                jb++;
            }
            else if (dp[ia + 1, jb] >= dp[ia, jb + 1])
            {
                edits.Add(('-', a[ia], ia, jb));
                ia++;
            }
            else
            {
                edits.Add(('+', b[jb], ia, jb));
                jb++;
            }
        }

        while (ia < n)
        {
            edits.Add(('-', a[ia], ia, jb));
            ia++;
        }

        while (jb < m)
        {
            edits.Add(('+', b[jb], ia, jb));
            jb++;
        }

        // Identify hunks: sequences with any +/-; include context around
        int idx = 0;
        while (idx < edits.Count)
        {
            // skip pure context
            while (idx < edits.Count && edits[idx].tag == ' ') idx++;
            if (idx >= edits.Count) break;
            int hunkStart = Math.Max(0, idx - context);
            int i2 = idx;
            int lastChange = idx;
            // extend until the next block of changes ends
            while (i2 < edits.Count)
            {
                if (edits[i2].tag != ' ') lastChange = i2;
                // if we have run past last change more than context, stop
                if (edits[i2].tag == ' ' && i2 - lastChange > context) break;
                i2++;
            }

            int hunkEnd = Math.Min(edits.Count, lastChange + context + 1);

            // Compute ranges for header
            int oldStart = 0, newStart = 0, oldCount = 0, newCount = 0;
            // derive starting line numbers by scanning from beginning counting only lines up to hunkStart
            int oldLine = 1, newLine = 1;
            for (int k = 0; k < hunkStart; k++)
            {
                if (edits[k].tag != '+') oldLine++;
                if (edits[k].tag != '-') newLine++;
            }

            oldStart = oldLine;
            newStart = newLine;
            // counts inside hunk
            for (int k = hunkStart; k < hunkEnd; k++)
            {
                if (edits[k].tag != '+') oldCount++;
                if (edits[k].tag != '-') newCount++;
            }

            lines.Add($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
            for (int k = hunkStart; k < hunkEnd; k++)
            {
                var (tag, text, _, __) = edits[k];
                lines.Add((tag == ' ' ? " " : tag.ToString()) + text);
            }

            idx = hunkEnd;
        }

        if (lines.Count <= 2)
        {
            lines.Add("Изменений нет");
        }

        return lines;
    }

    private void RenderDiff(IList<string> lines)
    {
        try
        {
            if (AiDiffBox == null) return;
            var doc = new FlowDocument();
            doc.PagePadding = new Thickness(4);
            doc.FontFamily = TryFindResource("MonospaceFont") as FontFamily ?? new FontFamily("Consolas");
            var textBrush = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
            var addBrush = new SolidColorBrush(Color.FromRgb(45, 160, 75));
            var delBrush = new SolidColorBrush(Color.FromRgb(200, 60, 60));
            var headBrush = TryFindResource("AccentHighlightBrush") as Brush ??
                            new SolidColorBrush(Color.FromRgb(50, 120, 200));
            var ctxBrush = TryFindResource("TextSecondaryBrush") as Brush ??
                           new SolidColorBrush(Color.FromRgb(120, 120, 120));

            foreach (var line in lines ?? Array.Empty<string>())
            {
                var para = new Paragraph { Margin = new Thickness(0), Padding = new Thickness(0) };
                if (line.StartsWith("@@"))
                {
                    var run = new Run(line) { Foreground = headBrush, FontWeight = FontWeights.Bold };
                    para.Inlines.Add(run);
                }
                else if (line.StartsWith("+++ ") || line.StartsWith("--- "))
                {
                    var run = new Run(line) { Foreground = headBrush };
                    para.Inlines.Add(run);
                }
                else if (line.StartsWith("+"))
                {
                    var run = new Run(line) { Foreground = addBrush };
                    para.Inlines.Add(run);
                }
                else if (line.StartsWith("-"))
                {
                    var run = new Run(line) { Foreground = delBrush };
                    para.Inlines.Add(run);
                }
                else if (line.StartsWith(" "))
                {
                    var run = new Run(line) { Foreground = ctxBrush };
                    para.Inlines.Add(run);
                }
                else
                {
                    var run = new Run(line) { Foreground = textBrush };
                    para.Inlines.Add(run);
                }

                doc.Blocks.Add(para);
            }

            AiDiffBox.Document = doc;
        }
        catch
        {
        }
    }

    private class AIConfig
    {
        public string OllamaApiKey { get; set; }
    }

    private void ShowTypingIndicator()
    {
        try
        {
            HideTypingIndicator();
            var bubble = new Border
            {
                Background = TryFindResource("BackgroundTertiaryBrush") as Brush,
                BorderBrush = TryFindResource("BorderBrush") as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 560
            };
            var stack = new StackPanel
                { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var pb = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 120, 
                Height = 12, 
                Margin = new Thickness(0, 0, 8, 0),
                Style = TryFindResource("ModernProgressBar") as Style
            };
            var txt = new TextBlock
            {
                Text = "AI думает...", VerticalAlignment = VerticalAlignment.Center,
                Foreground = TryFindResource("TextSecondaryBrush") as Brush
            };
            stack.Children.Add(pb);
            stack.Children.Add(txt);
            bubble.Child = stack;
            _aiTypingBubble = bubble;
            AiMessagesPanel.Children.Add(bubble);
            ScrollChatToBottom();
        }
        catch
        {
        }
    }

    private void HideTypingIndicator()
    {
        try
        {
            if (_aiTypingBubble != null)
            {
                AiMessagesPanel.Children.Remove(_aiTypingBubble);
                _aiTypingBubble = null;
            }
        }
        catch
        {
        }
    }
}

internal class BreakpointBackgroundRenderer : IBackgroundRenderer
{
    private readonly ISet<int> _breakpoints;
    private readonly Brush _fill;
    private readonly Pen _borderPen;

    public BreakpointBackgroundRenderer(ISet<int> breakpoints)
    {
        _breakpoints = breakpoints;
        _fill = new SolidColorBrush(Color.FromArgb(60, 220, 60, 60)); // semi-transparent red
        _fill.Freeze();
        _borderPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 200, 50, 50)), 1.0);
        _borderPen.Freeze();
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView?.VisualLines == null || textView.VisualLines.Count == 0) return;

        foreach (var vl in textView.VisualLines)
        {
            // The visual line can span multiple document lines due to folding/wrapping; iterate each doc line inside
            for (var docLine = vl.FirstDocumentLine;
                 docLine != null && docLine.Offset <= vl.LastDocumentLine.EndOffset;
                 docLine = docLine.NextLine)
            {
                if (_breakpoints.Contains(docLine.LineNumber))
                {
                    foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, docLine))
                    {
                        if (rect.Width <= 0 || rect.Height <= 0) continue;
                        // Expand to full editor width so the whole line is tinted
                        var full = new Rect(0, rect.Top, Math.Max(textView.ActualWidth, rect.Right), rect.Height);
                        drawingContext.DrawRectangle(_fill, _borderPen, full);
                    }
                }

                if (docLine == vl.LastDocumentLine) break;
            }
        }
    }
}

internal sealed class ApiSymbol
{
    public string Name { get; }
    public string Signature { get; }
    public string Description { get; }

    public ApiSymbol(string name, string signature, string description)
    {
        Name = name;
        Signature = signature;
        Description = description;
    }
}

internal class SignatureCompletionData : ICompletionData
{
    public SignatureCompletionData(ApiSymbol api)
    {
        Api = api;
        Text = api.Name; // used for filtering
        Content = api.Signature; // shown in the list
        Description = string.IsNullOrWhiteSpace(api.Description)
            ? api.Signature
            : api.Description + "\n" + api.Signature;
    }

    public ApiSymbol Api { get; }

    public ImageSource Image => null;
    public string Text { get; }
    public object Content { get; }
    public object Description { get; }
    public double Priority { get; set; }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Insert full signature/snippet; support cursor marker for better UX in callbacks
        var raw = Api.Signature ?? Api.Name;
        var cursorMarker = "__CURSOR__";
        var markerIndex = raw.IndexOf(cursorMarker, StringComparison.Ordinal);
        var insert = markerIndex >= 0 ? raw.Replace(cursorMarker, string.Empty) : raw;

        textArea.Document.Replace(completionSegment, insert);

        int desiredOffset;
        if (markerIndex >= 0)
        {
            // Place caret at the cursor marker position
            desiredOffset = completionSegment.Offset + markerIndex;
        }
        else
        {
            // Fallback: place caret right after '(' if there are parameters
            int open = insert.IndexOf('(');
            int close = insert.LastIndexOf(')');
            if (open >= 0)
            {
                var target = completionSegment.Offset + Math.Min(open + 1, insert.Length);
                desiredOffset = (close > open + 1) ? target : completionSegment.Offset + insert.Length;
            }
            else
            {
                desiredOffset = completionSegment.Offset + insert.Length;
            }
        }

        // Clamp caret offset to document bounds to avoid ArgumentOutOfRangeException
        var docLen = textArea.Document?.TextLength ?? 0;
        var clamped = Math.Max(0, Math.Min(docLen, desiredOffset));
        textArea.Caret.Offset = clamped;
    }
}

internal class FieldCompletionData : ICompletionData
{
    public FieldCompletionData(string fieldName, string ownerType)
    {
        Text = fieldName;
        Content = fieldName;
        Description = $"{ownerType}.{fieldName}";
    }

    public ImageSource Image => null;
    public string Text { get; }
    public object Content { get; }
    public object Description { get; }
    public double Priority { get; set; }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Just insert field name
        textArea.Document.Replace(completionSegment, Text);
        // place caret after the field name
        var desiredOffset = completionSegment.Offset + Text.Length;
        var docLen = textArea.Document?.TextLength ?? 0;
        textArea.Caret.Offset = Math.Max(0, Math.Min(docLen, desiredOffset));
    }
}