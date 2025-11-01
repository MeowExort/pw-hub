using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Services;
using Pw.Hub.Tools;

namespace Pw.Hub.ViewModels;

/// <summary>
/// ViewModel для окна редактора Lua.
/// Содержит состояние текста скрипта, брейкпоинтов, логов и команды запуска/отладки/остановки.
/// Представление отвечает за визуальные аспекты (AvalonEdit, маркеры брейкпоинтов и т.п.).
/// </summary>
public class LuaEditorViewModel : BaseViewModel
{
    private readonly ILuaDebugService _debugService;
    private readonly IUiDialogService _dialogs;

    private LuaScriptRunner _runner; // задаётся из окна через SetRunner

    public LuaEditorAiViewModel Ai { get; } = new LuaEditorAiViewModel();

    /// <summary>
    /// Определения входных параметров для запуска/отладки текущего скрипта.
    /// Если коллекция не пуста, перед запуском будет показан диалог ввода значений.
    /// </summary>
    public ObservableCollection<InputDefinitionDto> Inputs { get; } = new();

    public LuaEditorViewModel() : this(new LuaDebugService(), new UiDialogService()) { }

    public LuaEditorViewModel(ILuaDebugService debugService, IUiDialogService dialogs)
    {
        _debugService = debugService ?? new LuaDebugService();
        _dialogs = dialogs ?? new UiDialogService();

        RunCommand = new RelayCommand(_ => Run(), _ => CanRun);
        DebugCommand = new RelayCommand(_ => Debug(), _ => CanRun && Breakpoints.Count > 0);
        StopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
        ToggleBreakpointCommand = new RelayCommand(p => ToggleBreakpoint(p), _ => !IsRunning);
        ClearLogCommand = new RelayCommand(_ => LogText = string.Empty, _ => !IsRunning);

        // Настройка под‑VM AI: делегаты доступа к коду
        Ai.GetCurrentCode = () => Code;
        Ai.ApplyCode = code => { Code = (code ?? string.Empty).Replace("\r\n", "\n"); };
    }

    /// <summary>
    /// Назначает раннер Lua для выполнения/отладки.
    /// Вызывается из окна после инициализации UI.
    /// </summary>
    public void SetRunner(LuaScriptRunner runner)
    {
        _runner = runner;
    }

    private string _code = string.Empty;
    /// <summary>
    /// Текст Lua-кода. Должен быть привязан к редактору во View.
    /// </summary>
    public string Code
    {
        get => _code;
        set { _code = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(CanRun)); }
    }

    private bool _isRunning;
    /// <summary>
    /// Признак, что код выполняется/отлаживается в текущий момент.
    /// Управляет доступностью команд.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set { _isRunning = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    /// <summary>
    /// Можно ли запустить/отладить текущий код (не пустой и есть раннер).
    /// </summary>
    public bool CanRun => !string.IsNullOrWhiteSpace(Code) && _runner != null && !IsRunning;

    /// <summary>
    /// Набор брейкпоинтов (строки 1-based). View отвечает за визуализацию маркеров и клики по полям.
    /// </summary>
    public ObservableCollection<int> Breakpoints { get; } = new();

    private string _logText = string.Empty;
    /// <summary>
    /// Текст лога вывода (print/ошибки). View может привязать к многострочному TextBox.
    /// </summary>
    public string LogText
    {
        get => _logText;
        set { _logText = value ?? string.Empty; OnPropertyChanged(); }
    }

    // Команды управления
    public ICommand RunCommand { get; }
    public ICommand DebugCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ToggleBreakpointCommand { get; }
    public ICommand ClearLogCommand { get; }

    /// <summary>
    /// Событие-запрос: открыть окно просмотра переменных при остановке на брейкпоинте.
    /// View подпишется и покажет DebugVariablesWindow.
    /// </summary>
    public event Action<int, IDictionary<string, object>, IDictionary<string, object>> RequestOpenDebugVariables;

    /// <summary>
    /// Переключает брейкпоинт на заданной строке (параметр: int или строка).
    /// </summary>
    private void ToggleBreakpoint(object parameter)
    {
        try
        {
            var line = parameter switch
            {
                int i => i,
                string s when int.TryParse(s, out var v) => v,
                _ => -1
            };
            if (line <= 0) return;
            if (Breakpoints.Contains(line)) Breakpoints.Remove(line); else Breakpoints.Add(line);
        }
        catch { }
        finally { CommandManager.InvalidateRequerySuggested(); }
    }

    /// <summary>
    /// Останавливает выполнение кода, если это поддерживается раннером.
    /// </summary>
    private void Stop()
    {
        try { _runner?.Stop(); AppendLog("[Остановлено пользователем]"); }
        catch { }
        finally { IsRunning = false; }
    }

    /// <summary>
    /// Запуск Lua-кода без брейкпоинтов.
    /// </summary>
    private async void Run()
    {
        if (!CanRun) return;

        // Сбор аргументов запуска (если заданы входные параметры)
        Dictionary<string, object> args = new();
        try
        {
            if (Inputs != null && Inputs.Count > 0)
            {
                var collected = _dialogs.AskRunArguments(Inputs.ToList());
                if (collected == null) return; // пользователь отменил запуск
                args = collected;
            }
        }
        catch { }

        IsRunning = true;
        try
        {
            // Подключаем обработчики вывода
            _runner.SetPrintSink(AppendLog);
            _runner.SetProgressSink((p, m) => { /* editor run: можно игнорировать */ });
            await _runner.RunCodeAsync(Code, args);
        }
        catch (Exception ex)
        {
            _dialogs.Alert("Ошибка запуска: " + ex.Message, "Lua");
            AppendLog("[Ошибка] " + ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Отладка Lua-кода с брейкпоинтами. При остановке показывает окно переменных через событие.
    /// </summary>
    private async void Debug()
    {
        if (!CanRun || Breakpoints.Count == 0) return;

        // Сбор аргументов запуска (если заданы входные параметры)
        Dictionary<string, object> args = new();
        try
        {
            if (Inputs != null && Inputs.Count > 0)
            {
                var collected = _dialogs.AskRunArguments(Inputs.ToList());
                if (collected == null) return; // отмена
                args = collected;
            }
        }
        catch { }

        IsRunning = true;
        try
        {
            LuaScriptRunner.DebugBreakHandler onBreak = (line, locals, globals) =>
            {
                try { RequestOpenDebugVariables?.Invoke(line, locals, globals); }
                catch { }
                return true; // продолжать выполнение после закрытия окна
            };
            await _debugService.RunWithBreakpointsAsync(_runner, Code, new HashSet<int>(Breakpoints), onBreak, args);
        }
        catch (Exception ex)
        {
            _dialogs.Alert("Ошибка отладки: " + ex.Message, "Lua");
            AppendLog("[Ошибка] " + ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Добавляет строку в лог с переводом строки.
    /// </summary>
    private void AppendLog(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!string.IsNullOrEmpty(LogText)) LogText += Environment.NewLine;
        LogText += text;
        OnPropertyChanged(nameof(LogText));
    }
}
