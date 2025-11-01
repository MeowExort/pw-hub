using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Services;

namespace Pw.Hub.ViewModels;

/// <summary>
/// ViewModel окна редактора модуля (API).
/// Содержит состояние полей (Название, Версия, Описание, Скрипт) и коллекцию входных параметров.
/// Инкапсулирует команды "Сохранить" и "Добавить параметр" и формирование запроса к API.
/// Визуально-специфичные аспекты (WebView2 превью, кнопки форматирования Markdown и т.п.) остаются во View.
/// </summary>
public class ModulesApiEditorViewModel : INotifyPropertyChanged
{
    private readonly IAiDocService _aiDoc;
    private bool _isBusy;

    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }

    private string _name = string.Empty;
    /// <summary>
    /// Название модуля.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; OnPropertyChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
    }

    private string _version = "1.0.0";
    /// <summary>
    /// Версия модуля в формате SemVer.
    /// </summary>
    public string Version
    {
        get => _version;
        set { _version = string.IsNullOrWhiteSpace(value) ? "1.0.0" : value.Trim(); OnPropertyChanged(); }
    }

    private string _description = string.Empty;
    /// <summary>
    /// Описание модуля в Markdown.
    /// </summary>
    public string Description
    {
        get => _description;
        set { _description = value ?? string.Empty; OnPropertyChanged(); }
    }

    private string _script = string.Empty;
    /// <summary>
    /// Текст Lua-скрипта модуля.
    /// </summary>
    public string Script
    {
        get => _script;
        set { _script = value ?? string.Empty; OnPropertyChanged(); }
    }

    /// <summary>
    /// Набор входных параметров модуля для формы аргументов.
    /// </summary>
    public ObservableCollection<InputItem> Inputs { get; } = new();

    /// <summary>
    /// Флаг, что пользователь подтвердил сохранение (используется окном).
    /// </summary>
    public bool IsSaved { get; private set; }

    // Команды
    public ICommand AddInputCommand { get; }
    public ICommand RemoveInputCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand GenerateDescriptionCommand { get; }

    /// <summary>
    /// Текущий выбранный входной параметр (для удаления/редактирования).
    /// </summary>
    public InputItem? SelectedInput
    {
        get => _selectedInput;
        set { _selectedInput = value; OnPropertyChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
    }
    private InputItem? _selectedInput;

    /// <summary>
    /// Событие запроса закрытия окна (true/false для DialogResult).
    /// </summary>
    public event Action<bool?> RequestClose;

    public ModulesApiEditorViewModel()
    {
        _aiDoc = (App.Services?.GetService(typeof(IAiDocService)) as IAiDocService) ?? new AiDocService();

        AddInputCommand = new RelayCommand(_ =>
        {
            var item = new InputItem
            {
                Name = "param",
                Label = "Параметр",
                Type = "string",
                Default = string.Empty,
                Required = false
            };
            Inputs.Add(item);
            SelectedInput = item;
        });
        RemoveInputCommand = new RelayCommand(_ =>
        {
            if (SelectedInput != null)
            {
                var idx = Inputs.IndexOf(SelectedInput);
                Inputs.Remove(SelectedInput);
                if (Inputs.Count > 0)
                {
                    SelectedInput = Inputs[Math.Clamp(idx - 1, 0, Inputs.Count - 1)];
                }
                else
                {
                    SelectedInput = null;
                }
            }
        }, _ => SelectedInput != null);
        SaveCommand = new RelayCommand(_ =>
        {
            IsSaved = true;
            RequestClose?.Invoke(true);
        }, _ => CanSave());
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

        GenerateDescriptionCommand = new RelayCommand(async _ => await GenerateDescriptionAsync(), _ => CanGenerateDescription());
    }

    /// <summary>
    /// Простая проверка возможности сохранения: имя и скрипт обязательны.
    /// </summary>
    private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Script);

    private bool CanGenerateDescription() => !IsBusy && !string.IsNullOrWhiteSpace(Name);

    private async Task GenerateDescriptionAsync()
    {
        if (!CanGenerateDescription()) return;

        // Проверяем наличие AI ключа и при необходимости предлагаем настроить
        var cfgSvc = (App.Services?.GetService(typeof(IAiConfigService)) as IAiConfigService) ?? new AiConfigService();
        var eff = cfgSvc.GetEffective();
        var key = (eff.ApiKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            try
            {
                var winSvc = App.Services?.GetService(typeof(IWindowService)) as IWindowService;
                var dlg = new Pw.Hub.Windows.AiApiKeyWindow();
                if (winSvc != null)
                {
                    var owner = System.Windows.Application.Current?.MainWindow;
                    winSvc.ShowDialog(dlg, owner);
                }
                else
                {
                    dlg.Owner = System.Windows.Application.Current?.MainWindow;
                    dlg.ShowDialog();
                }
            }
            catch { }

            eff = cfgSvc.GetEffective();
            key = (eff.ApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                // Не блокируем UI, просто информируем пользователя обновив описание
                Description = (Description ?? string.Empty) + (string.IsNullOrEmpty(Description) ? string.Empty : "\n\n") + "[AI] Укажите API key в настройках AI, чтобы сгенерировать описание.";
                return;
            }
        }

        IsBusy = true;
        try
        {
            var inputs = Inputs.Select(i => (i.Name ?? string.Empty, i.Type ?? "string", string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label)).ToList();
            var scriptFrag = Script ?? string.Empty;
            var md = await _aiDoc.GenerateDescriptionAsync(Name?.Trim() ?? string.Empty, Version?.Trim() ?? "1.0.0", inputs, scriptFrag);
            Description = md ?? string.Empty;
        }
        catch (Exception ex)
        {
            Description = (Description ?? string.Empty) + (string.IsNullOrEmpty(Description) ? string.Empty : "\n\n") + "[Ошибка генерации описания] " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Формирует DTO-запрос к Modules API на создание/обновление модуля из текущего состояния VM.
    /// </summary>
    public CreateOrUpdateModule BuildRequest()
    {
        return new CreateOrUpdateModule
        {
            Name = Name?.Trim() ?? string.Empty,
            Version = string.IsNullOrWhiteSpace(Version) ? "1.0.0" : Version.Trim(),
            Description = Description ?? string.Empty,
            Script = Script ?? string.Empty,
            Inputs = Inputs.Select(i => new InputDefinitionDto
            {
                Name = i.Name ?? string.Empty,
                Label = string.IsNullOrWhiteSpace(i.Label) ? (i.Name ?? string.Empty) : i.Label,
                Type = string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type,
                Default = i.Default,
                Required = i.Required
            }).ToArray()
        };
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Модель строки для таблицы входных параметров (редактируемая в гриде).
    /// </summary>
    public class InputItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _label = string.Empty;
        private string _type = "string";
        private string _default = string.Empty;
        private bool _required;

        public string Name { get => _name; set { _name = value ?? string.Empty; OnPropertyChanged(); } }
        public string Label { get => _label; set { _label = value ?? string.Empty; OnPropertyChanged(); } }
        public string Type { get => _type; set { _type = string.IsNullOrWhiteSpace(value) ? "string" : value.Trim(); OnPropertyChanged(); } }
        public string Default { get => _default; set { _default = value ?? string.Empty; OnPropertyChanged(); } }
        public bool Required { get => _required; set { _required = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
