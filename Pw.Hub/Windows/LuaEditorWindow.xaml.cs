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

namespace Pw.Hub.Windows;

public partial class LuaEditorWindow : Window
{
    private readonly LuaScriptRunner _runner;
    private CompletionWindow _completionWindow;
    private readonly HashSet<int> _breakpoints = new();
    private BreakpointBackgroundRenderer _bpRenderer;

    // Simple type model for Lua API objects (for autocomplete after '.')
    private sealed class ObjectType
    {
        public string Name { get; }
        public string[] Fields { get; }
        public ObjectType(string name, params string[] fields)
        {
            Name = name; Fields = fields ?? Array.Empty<string>();
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
        { "accounts", AccountType },    // treat as element type for convenience
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
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
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
        catch { }

        UpdateBpCount();
    }

    private void OnClosed(object sender, EventArgs e)
    {
        // Stop routing Print to this window
        _runner.SetPrintSink(null);
        // Dispose current Lua VM used by editor so pending callbacks are cancelled and resources released
        try { _runner.Stop(); } catch { }
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
            catch { }

            var data = _completionWindow.CompletionList.CompletionData;
            data.Clear();

            var source = (memberItems != null && memberItems.Count > 0) ? memberItems : _cachedCompletionItems;
            foreach (var item in source)
                data.Add(item);

            _completionWindow.Closed += (_, _) => _completionWindow = null;
            _completionWindow.Show();
        }
        catch { }
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
                if (char.IsLetterOrDigit(ch) || ch == '_') idStart--; else break;
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
                    var m1 = Regex.Match(window1, @"function\s*\(\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\)\s*$", RegexOptions.RightToLeft);
                    if (m1.Success)
                    {
                        var paramName1 = m1.Groups[1].Value;
                        if (string.Equals(paramName1, ident, StringComparison.Ordinal))
                        {
                            // Try to see preceding API name in the same window
                            var apiMatch1 = Regex.Match(window1, @"([A-Za-z_][A-Za-z0-9_]*)\s*\(.*function\s*\(\s*" + Regex.Escape(paramName1) + @"\s*\)", RegexOptions.Singleline);
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
                        if (char.IsLetterOrDigit(ch2) || ch2 == '_') idStart2--; else break;
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
        catch { return null; }
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
            var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
            if (OutputBox.Text.Length == 0)
                OutputBox.Text = line;
            else
                OutputBox.AppendText("\r\n" + line);
            OutputBox.ScrollToEnd();
        }
        catch { }
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
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
    }

    private async void OnDebugClick(object sender, RoutedEventArgs e)
    {
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
        catch { }
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
        catch { return value?.ToString() ?? "nil"; }
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
        catch { }
    }

    private void InvalidateBreakpointMarks()
    {
        try { Editor?.TextArea?.TextView?.InvalidateLayer(KnownLayer.Background); } catch { }
    }

    private void UpdateBpCount()
    {
        try
        {
            if (BpCountText != null)
                BpCountText.Text = $"({_breakpoints.Count} точек)";
        }
        catch { }
    }

    private void OnClearOutputClick(object sender, RoutedEventArgs e)
    {
        OutputBox.Clear();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
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
            for (var docLine = vl.FirstDocumentLine; docLine != null && docLine.Offset <= vl.LastDocumentLine.EndOffset; docLine = docLine.NextLine)
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
        Description = string.IsNullOrWhiteSpace(api.Description) ? api.Signature : api.Description + "\n" + api.Signature;
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
