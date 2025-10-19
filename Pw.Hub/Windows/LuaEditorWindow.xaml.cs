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

namespace Pw.Hub.Windows;

public partial class LuaEditorWindow : Window
{
    private readonly LuaScriptRunner _runner;
    private CompletionWindow? _completionWindow;
    private readonly string[] _apiSymbols = new[]
    {
        // Callback preferred APIs
        "Account_GetAccountCb","Account_IsAuthorizedCb","Account_GetAccountsJsonCb","Account_GetAccountsCb","Account_ChangeAccountCb",
        "Browser_NavigateCb","Browser_ReloadCb","Browser_ExecuteScriptCb","Browser_ElementExistsCb","Browser_WaitForElementCb","Browser_GetCookiesJsonCb","Browser_SetCookiesJsonCb",
        // Synchronous variants
        "Account_GetAccount","Account_IsAuthorized","Account_GetAccountsJson","Account_GetAccounts",
        "Browser_Navigate","Browser_Reload","Browser_ExecuteScript","Browser_ElementExists","Browser_WaitForElement","Browser_GetCookiesJson","Browser_SetCookiesJson",
        // Helpers / globals
        "Print","Sleep","selectedAccountId"
    };

    public LuaEditorWindow(LuaScriptRunner runner)
    {
        _runner = runner;
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Route Lua Print to our output box
        _runner.SetPrintSink(AppendLog);

        // Syntax highlighting
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var xshdPath = Path.Combine(baseDir, "Assets", "LuaEditor", "lua.xshd");
            if (File.Exists(xshdPath))
            {
                using var s = File.OpenRead(xshdPath);
                using var reader = new XmlTextReader(s);
                Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
        catch { }

        // Editor template if empty
        if (Editor != null && string.IsNullOrWhiteSpace(Editor.Text))
        {
            Editor.Text = "-- Lua script\n-- Use Account_*Cb and Browser_*Cb APIs\nPrint('Hello from editor')\n";
        }

        // Autocomplete hooks
        Editor.TextArea.TextEntering += TextAreaOnTextEntering;
        Editor.TextArea.TextEntered += TextAreaOnTextEntered;
        Editor.PreviewKeyDown += EditorOnPreviewKeyDown;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Stop routing Print to this window
        _runner.SetPrintSink(null);
    }

    private void EditorOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ShowCompletion();
            e.Handled = true;
        }
    }

    private void TextAreaOnTextEntered(object? sender, TextCompositionEventArgs e)
    {
        // Trigger on dot or after letters to help discoverability
        if (char.IsLetterOrDigit(e.Text.Last()) || e.Text == "_" || e.Text == ".")
        {
            ShowCompletion();
        }
    }

    private void TextAreaOnTextEntering(object? sender, TextCompositionEventArgs e)
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
            _completionWindow = new CompletionWindow(Editor.TextArea);
            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var s in _apiSymbols.Distinct())
            {
                data.Add(new SimpleCompletionData(s));
            }
            _completionWindow.SizeToContent = SizeToContent.WidthAndHeight;
            _completionWindow.Show();
            _completionWindow.Closed += (_, _) => _completionWindow = null;
        }
        catch { }
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
            AppendLog("Запуск скрипта...");
            var code = Editor?.Text ?? string.Empty;
            await _runner.RunCodeAsync(code);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось выполнить скрипт: {ex.Message}", "Lua", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

internal class SimpleCompletionData : ICompletionData
{
    public SimpleCompletionData(string text)
    {
        Text = text;
        Content = text;
        Description = text;
    }

    public System.Windows.Media.ImageSource Image => null;
    public string Text { get; }
    public object Content { get; }
    public object Description { get; }
    public double Priority { get; set; }
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}
