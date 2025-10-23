using System.IO;
using System.Windows;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Controls;
using System.Linq;

namespace Pw.Hub.Windows;

public partial class AIGeneratorWindow : Window
{
    private readonly HttpClient _httpClient;
    private const string OllamaCloudUrl = "https://ollama.com/api/chat"; // Правильный endpoint
    private string _apiKey = "your-ollama-cloud-api-key";

    public AIGeneratorWindow()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
        
        InitializeApiKey();
    }

    private void InitializeApiKey()
    {
        _apiKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY") ?? _apiKey;
        
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config", "ai_settings.json");
            if (File.Exists(configPath))
            {
                var config = JsonSerializer.Deserialize<AIConfig>(File.ReadAllText(configPath));
                _apiKey = config?.OllamaApiKey ?? _apiKey;
            }
        }
        catch
        {
            // Игнорируем ошибки чтения конфига
        }
    }

    private async void GenerateLua_Click(object sender, RoutedEventArgs e)
    {
        var prompt = (PromptText.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MessageBox.Show("Введите описание для генерации скрипта.", "AI Генератор", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your-ollama-cloud-api-key")
        {
            MessageBox.Show("API ключ Ollama Cloud не настроен. Пожалуйста, настройте API ключ в настройках приложения.", 
                "AI Генератор", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            LuaPreview.Text = "Подключение к Ollama Cloud...";
            GenerateLuaBtn.Content = "Генерация...";
            GenerateLuaBtn.IsEnabled = false;

            var generatedCode = await GenerateWithOllamaCloud(prompt);
            LuaPreview.Text = generatedCode;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка генерации кода: {ex.Message}", "AI Генератор", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            LuaPreview.Text = BuildFallbackLua(prompt) + $"\n\n-- Ошибка: {ex.Message}";
        }
        finally
        {
            GenerateLuaBtn.Content = "Сгенерировать";
            GenerateLuaBtn.IsEnabled = true;
        }
    }

    // Добавляем обработчик для кнопки настроек
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new ApiKeySettingsWindow(_apiKey);
        if (settingsWindow.ShowDialog() == true)
        {
            _apiKey = settingsWindow.ApiKey;
            MessageBox.Show("API ключ обновлен", "Настройки", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task<string> GenerateWithOllamaCloud(string userPrompt)
    {
        var systemPrompt = BuildSystemPrompt();
        
        var requestBody = new
        {
            model = "deepseek-v3.1:671b", // Можно использовать "gpt-oss:120b" или другие доступные модели
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.PostAsync(OllamaCloudUrl, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Ошибка API ({response.StatusCode}): {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var aiResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return aiResponse?.Message?.Content?.Trim() ?? BuildFallbackLua(userPrompt);
    }

    private string BuildSystemPrompt()
    {
        return @"Ты - эксперт по Lua скриптам для автоматизации игровых процессов. Сгенерируй чистый, рабочий код на Lua.

ДОСТУПНОЕ API (все функции асинхронные с callback):

=== РАБОТА С АККАУНТАМИ ===
Account_GetAccountCb(cb) - получить текущий аккаунт
Account_GetAccountsCb(cb) - получить все аккаунты (возвращает таблицу с полями: Id, Name, Email, Servers)
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

=== СТРУКТУРА ДАННЫХ ===
Аккаунт: {Id, Name, Email, Servers[]}
Сервер: {Id, Name, Characters[]}
Персонаж: {Id, Name}

ВАЖНЫЕ ПРАВИЛА:
1. ВСЕГДА используй асинхронные версии функций (оканчиваются на Cb)
2. Добавляй комментарии на русском языке для основных блоков
3. Обрабатывай возможные ошибки и пограничные случаи
4. Используй понятные именования переменных
5. Логируй ключевые этапы через Print()
6. Если функция Complete доступна - вызывай ее в конце
7. Для работы с таблицами используй ipairs и # для размера
8. Всегда проверяй существование данных перед использованием

Сгенерируй готовый к использованию Lua скрипт с правильной структурой и обработкой ошибок.";
    }

    private string BuildFallbackLua(string prompt)
    {
        var escaped = prompt.Replace("'", "\\'").Replace("\"", "\\\"");
        return $@"-- Автоматически сгенерированный скрипт
-- Задача: {escaped}

local function main()
    Print('Запуск скрипта для: ""{escaped}""')
    ReportProgress(10)
    
    -- Получение списка аккаунтов
    Account_GetAccountsCb(function(accounts)
        if accounts == nil then
            Print('Ошибка: не удалось получить аккаунты')
            if Complete ~= nil then
                Complete('Ошибка получения аккаунтов')
            end
            return
        end
        
        ReportProgress(60)
        
        -- Обработка результатов
        local accountCount = #accounts
        if accountCount == 0 then
            Print('Аккаунты не найдены')
        else
            -- Вывод информации об аккаунтах
            Print('Найдено аккаунтов: ' .. tostring(accountCount))
            for i = 1, accountCount do
                local acc = accounts[i]
                local name = acc.Name or 'Без имени'
                local email = acc.Email or 'Нет email'
                Print(string.format('Аккаунт %d: %s (%s)', i, name, email))
                
                -- Пример обработки серверов аккаунта
                if acc.Servers and #acc.Servers > 0 then
                    Print('  Серверы: ' .. tostring(#acc.Servers))
                end
            end
        end
        
        ReportProgress(100)
        
        -- Завершение работы
        if Complete ~= nil then
            Complete('Успешно завершено: обработано ' .. tostring(accountCount) .. ' аккаунтов')
        else
            Print('Скрипт завершен')
        end
    end)
end

-- Запуск основной функции
main()";
    }

    private void SaveLua_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var scriptsDir = Path.Combine(baseDir, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            
            var fileName = MakeSafeFileName(string.IsNullOrWhiteSpace(PromptText.Text) ? 
                "generated" : PromptText.Text);
            if (fileName.Length > 40) fileName = fileName.Substring(0, 40);
            
            var fullPath = Path.Combine(scriptsDir, fileName + ".lua");
            File.WriteAllText(fullPath, LuaPreview.Text, Encoding.UTF8);
            
            MessageBox.Show($"Скрипт сохранён: {fullPath}", "AI Генератор", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения скрипта: {ex.Message}", "AI Генератор", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string MakeSafeFileName(string text)
    {
        var s = new string(text.Where(ch => 
            char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == ' ').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(s)) s = "generated";
        s = s.Replace(' ', '_').ToLowerInvariant();
        return s;
    }

    // Обновленные классы для десериализации ответа Ollama Chat API
    private class OllamaChatResponse
    {
        public string Model { get; set; }
        public Message Message { get; set; }
        public bool Done { get; set; }
    }

    private class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    private class AIConfig
    {
        public string OllamaApiKey { get; set; }
    }
}