using System.IO;
using System.Text.Json;
using System.Windows;

namespace Pw.Hub.Windows;

public partial class ApiKeySettingsWindow : Window
{
    public string ApiKey { get; private set; }

    public ApiKeySettingsWindow(string currentApiKey)
    {
        InitializeComponent();
        ApiKeyBox.Password = currentApiKey ?? "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ApiKey = ApiKeyBox.Password.Trim();
        
        // Сохраняем в конфиг
        try
        {
            var configDir = Path.Combine(AppContext.BaseDirectory, "config");
            Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, "ai_settings.json");
            
            var config = new { OllamaApiKey = ApiKey };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}