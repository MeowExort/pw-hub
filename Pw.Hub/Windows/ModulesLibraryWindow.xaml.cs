using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Pw.Hub.Services;
using Markdig;

namespace Pw.Hub.Windows
{
    public partial class ModulesLibraryWindow : Window
    {
        private readonly ModulesApiClient _api;
        private ModuleDto? _selected;
        private readonly string _userId;

        public ModulesLibraryWindow(string? apiBaseUrl = null, string? userId = null)
        {
            InitializeComponent();
            _api = new ModulesApiClient(apiBaseUrl);
            _userId = string.IsNullOrWhiteSpace(userId) ? "local-user" : userId!;
            Loaded += async (_, _) => await InitAsync();
        }

        private async Task InitAsync()
        {
            try
            {
                // Ensure WebView2 is initialized
                if (DescriptionWebView.CoreWebView2 == null)
                {
                    await DescriptionWebView.EnsureCoreWebView2Async();
                }
            }
            catch
            {
                // ignore, we'll try to load anyway
            }

            await SearchAndBindAsync();
        }

        private async Task SearchAndBindAsync()
        {
            try
            {
                var sort = (SortCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                var order = (OrderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                var resp = await _api.SearchAsync(SearchTextBox.Text, TagsTextBox.Text, sort, order, 1, 50);
                ModulesList.ItemsSource = resp.Items;
                if (resp.Items.Count > 0)
                {
                    ModulesList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка загрузки библиотеки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e)
        {
            await SearchAndBindAsync();
        }

        private void ModulesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = ModulesList.SelectedItem as ModuleDto;
            TitleText.Text = _selected?.Name ?? string.Empty;
            var md = _selected?.Description ?? string.Empty;
            var html = string.IsNullOrWhiteSpace(md) ? "<i>Нет описания</i>" : Markdown.ToHtml(md);
            TrySetHtml(html);
        }

        private async void TrySetHtml(string html)
        {
            try
            {
                if (DescriptionWebView.CoreWebView2 == null)
                    await DescriptionWebView.EnsureCoreWebView2Async();

                var doc = "<!DOCTYPE html><html><head><meta charset='utf-8'><style>body{font-family:Segoe UI,Arial,sans-serif;padding:12px} pre{background:#f6f8fa;padding:8px;border-radius:6px;overflow:auto} code{background:#f6f8fa;padding:2px 4px;border-radius:4px}</style></head><body>" + html + "</body></html>";
                DescriptionWebView.NavigateToString(doc);
            }
            catch
            {
                // ignore rendering issues
            }
        }

        private async void OnInstallClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try
            {
                var res = await _api.InstallAsync(_selected.Id, _userId);
                if (res != null)
                {
                    _selected.InstallCount = res.InstallCount;
                    ModulesList.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Не удалось установить модуль: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnUninstallClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try
            {
                var res = await _api.UninstallAsync(_selected.Id, _userId);
                if (res != null)
                {
                    _selected.InstallCount = res.InstallCount;
                    ModulesList.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Не удалось удалить модуль: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}