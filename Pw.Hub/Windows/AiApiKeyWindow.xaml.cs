using System;
using System.Windows;

namespace Pw.Hub.Windows
{
    public partial class AiApiKeyWindow : Window
    {
        private readonly Pw.Hub.Services.IAiConfigService _cfg;
        public AiApiKeyWindow()
        {
            InitializeComponent();
            _cfg = (Pw.Hub.App.Services?.GetService(typeof(Pw.Hub.Services.IAiConfigService)) as Pw.Hub.Services.IAiConfigService) ?? new Pw.Hub.Services.AiConfigService();
            try
            {
                var cfg = _cfg.Load();
                UrlBox.Text = cfg.ApiUrl ?? "https://ollama.com/api/chat";
                ModelBox.Text = cfg.Model ?? "llama3.1";
                // API ключ намеренно не отображаем
            }
            catch { }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch { }
            Close();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = (UrlBox.Text ?? string.Empty).Trim();
                var model = (ModelBox.Text ?? string.Empty).Trim();
                var key = (KeyBox.Password ?? string.Empty).Trim();

                var cfg = new Pw.Hub.Services.AiConfig
                {
                    ApiUrl = string.IsNullOrWhiteSpace(url) ? "https://ollama.com/api/chat" : url,
                    Model = string.IsNullOrWhiteSpace(model) ? "llama3.1" : model,
                    ApiKey = key
                };
                _cfg.Save(cfg);

                try { DialogResult = true; } catch { }
                Close();
            }
            catch
            {
                try { DialogResult = false; } catch { }
                Close();
            }
        }
    }
}