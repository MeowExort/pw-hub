using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Pw.Hub.Infrastructure;

namespace Pw.Hub.ViewModels;

/// <summary>
/// ViewModel окна настроек API-ключа. Содержит бизнес-логику сохранения ключа
/// и команды управления диалогом. Представление только отображает и инициирует команды.
/// </summary>
public class ApiKeySettingsViewModel : BaseViewModel
{
    private string _apiKey = string.Empty;

    /// <summary>
    /// Текущий API-ключ Ollama Cloud. Привязан к полю ввода в окне.
    /// </summary>
    public string ApiKey
    {
        get => _apiKey;
        set { _apiKey = value ?? string.Empty; OnPropertyChanged(); }
    }

    /// <summary>
    /// Команда сохранения ключа и закрытия диалога с результатом true.
    /// </summary>
    public ICommand SaveCommand { get; }

    /// <summary>
    /// Команда отмены и закрытия диалога с результатом false.
    /// </summary>
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Событие запроса закрытия окна. View подписывается и закрывает себя,
    /// так как только представление может установить DialogResult.
    /// </summary>
    public event Action<bool?> RequestClose;

    public ApiKeySettingsViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
    }

    /// <summary>
    /// Проверяет, можно ли сохранять (например, допускаем пустое значение — пользователь может очищать ключ).
    /// Здесь можно добавить валидацию формата.
    /// </summary>
    private bool CanSave() => true;

    /// <summary>
    /// Сохраняет ключ в локный конфиг и инициирует закрытие окна.
    /// </summary>
    private void Save()
    {
        try
        {
            var configDir = Path.Combine(AppContext.BaseDirectory, "config");
            Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, "ai_settings.json");

            var config = new { OllamaApiKey = ApiKey?.Trim() };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config));

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            // Пробрасываем исключение наружу для показа пользователю средствами View
            // (например, через MessageBox). Так VM остаётся независимой от UI API.
            throw new InvalidOperationException($"Ошибка сохранения настроек: {ex.Message}", ex);
        }
    }
}
