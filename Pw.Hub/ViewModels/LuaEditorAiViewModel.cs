using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Services;
using Pw.Hub.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Text;
using System.Text.RegularExpressions;

namespace Pw.Hub.ViewModels
{
    /// <summary>
    /// Под-VM для AI-панели редактора Lua. Управляет сообщениями чата, отправкой запросов,
    /// предпросмотром diff и применением изменений к редактору.
    /// </summary>
    public class LuaEditorAiViewModel : BaseViewModel
    {
        private readonly IAiAssistantService _ai;
        private readonly IDiffPreviewService _diff;

        public LuaEditorAiViewModel()
        {
            // Разрешаем сервис через DI с безопасным fallback
            _ai = (App.Services?.GetService(typeof(IAiAssistantService)) as IAiAssistantService)
                  ?? new AiAssistantService(new DiffPreviewService());
            _diff = (App.Services?.GetService(typeof(IDiffPreviewService)) as IDiffPreviewService)
                    ?? new DiffPreviewService();

            SendCommand = new RelayCommand(async _ => await SendAsync(), _ => CanSend);
            NewSessionCommand = new RelayCommand(_ => NewSession(), _ => !IsBusy);
            ApplyCommand = new RelayCommand(_ => Apply(), _ => CanApply);
            OpenFullDiffCommand = new RelayCommand(_ => RequestOpenFullDiff?.Invoke(DiffLines), _ => true);
        }

        // Делегаты связи с основным VM редактора
        public Func<string> GetCurrentCode { get; set; } = () => string.Empty;
        public Action<string> ApplyCode { get; set; } = _ => { };
        // Доступ к родительскому VM для отслеживания изменений кода
        internal LuaEditorViewModel ParentVm { get; set; }

        public ObservableCollection<AiChatMessage> Messages { get; } = new();

        public event Action<IList<string>> RequestOpenFullDiff;

        private IList<string> _diffLines = new List<string>();
        public IList<string> DiffLines { get => _diffLines; private set { _diffLines = value ?? new List<string>(); OnPropertyChanged(); UpdateApplyAvailability(); } }

        private string _inputText = string.Empty;
        public string InputText { get => _inputText; set { _inputText = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(CanSend)); CommandManager.InvalidateRequerySuggested(); } }

        private bool _braveMode;
        public bool BraveMode { get => _braveMode; set { _braveMode = value; OnPropertyChanged(); if (_braveMode) TryAutoApply(); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private bool _canApply;
        public bool CanApply { get => _canApply; private set { _canApply = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private string _lastProposedCode = string.Empty;
        private readonly StringBuilder _assistantRaw = new StringBuilder();
        private string _lastPreviewCode = string.Empty;

        public ICommand SendCommand { get; }
        public ICommand NewSessionCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand OpenFullDiffCommand { get; }

        private static string StripCodeBlocks(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            try
            {
                // Удаляем содержимое в тройных кавычках ```...```
                var res = Regex.Replace(text, "```[\u0000-\uFFFF]*?```", string.Empty, RegexOptions.Singleline);
                return res.Trim();
            }
            catch { return text; }
        }

        private static string ExtractLuaCodeBlock(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            try
            {
                var m = Regex.Match(text, "```lua\\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value.Trim();
                m = Regex.Match(text, "```\\s*(.*?)```", RegexOptions.Singleline);
                if (m.Success) return m.Groups[1].Value.Trim();
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        public void NotifySystem(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Messages.Add(new AiChatMessage { Role = "system", Text = text });
        }

        private bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

        private void AddSystem(string text) => Messages.Add(new AiChatMessage { Role = "system", Text = text });
        private void AddUser(string text) => Messages.Add(new AiChatMessage { Role = "user", Text = text });
        private void AddAssistant(string text) => Messages.Add(new AiChatMessage { Role = "assistant", Text = text });

        private void NewSession()
        {
            if (IsBusy) return;
            Messages.Clear();
            DiffLines = new List<string>();
            _lastProposedCode = string.Empty;
            _lastPreviewCode = string.Empty;
            _assistantRaw.Clear();
            _ai.NewSession();
            // В начало сессии добавляем краткую шпаргалку по доступному Lua API (v1/v2),
            // полученную из централизованного реестра
            try
            {
                var cheat = Infrastructure.LuaApiRegistry.ToCheatSheetText();
                if (!string.IsNullOrWhiteSpace(cheat))
                    AddSystem(cheat);
            }
            catch { }
            AddSystem("Новая сессия начата. Опишите задачу для AI.");
            RequeryCommands();
        }

        private async Task SendAsync(CancellationToken ct = default)
        {
            if (!CanSend) { RequeryCommands(); return; }

            IsBusy = true;
            try
            {
                // Проверка наличия AI ключа; если отсутствует — предлагаем настроить
                var cfgSvc = (App.Services?.GetService(typeof(IAiConfigService)) as IAiConfigService) ?? new AiConfigService();
                var eff = cfgSvc.GetEffective();
                var key = (eff.ApiKey ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    try
                    {
                        // Пытаемся показать окно настроек через сервис окон, если доступен
                        var winSvc = App.Services?.GetService(typeof(IWindowService)) as IWindowService;
                        if (winSvc != null)
                        {
                            var dlg = new AiApiKeyWindow();
                            var owner = System.Windows.Application.Current?.Windows?.OfType<System.Windows.Window>()?.FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current?.MainWindow;
                            winSvc.ShowDialog(dlg, owner);
                        }
                        else
                        {
                            // Прямой показ окна как fallback
                            var dlg = new AiApiKeyWindow { Owner = System.Windows.Application.Current?.MainWindow };
                            dlg.ShowDialog();
                        }
                    }
                    catch { }

                    // перечитываем ключ из сервиса конфигурации
                    eff = cfgSvc.GetEffective();
                    key = (eff.ApiKey ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        NotifySystem("AI API key не задан. Откройте настройки AI и введите ключ для продолжения.");
                        return;
                    }
                }

                // Очистка предыдущего состояния стрима
                _assistantRaw.Clear();
                _lastPreviewCode = string.Empty;
                DiffLines = new List<string>();

                var prompt = InputText.Trim();
                AddUser(prompt);
                InputText = string.Empty;

                var currentCode = (GetCurrentCode?.Invoke() ?? string.Empty).Replace("\r\n", "\n");
                
                // Вычисляем diff ручных правок для передачи AI в качестве контекста
                string manualChangesDiff = null;
                try
                {
                    if (ParentVm != null && !string.IsNullOrEmpty(ParentVm.LastCodeSentToAi))
                    {
                        var lastSent = ParentVm.LastCodeSentToAi.Replace("\r\n", "\n");
                        if (!string.Equals(currentCode, lastSent, StringComparison.Ordinal))
                        {
                            // Есть ручные правки - строим diff
                            var diffLines = _diff.BuildUnifiedDiffGit(lastSent, currentCode);
                            if (diffLines != null && diffLines.Count > 0)
                            {
                                manualChangesDiff = string.Join("\n", diffLines);
                            }
                        }
                    }
                }
                catch { }
                // Создаём один бабл ассистента и будем наполнять его текст по мере поступления стрима
                var assistantMsg = new AiChatMessage { Role = "assistant", Text = string.Empty, ShowDiffButton = true };
                Messages.Add(assistantMsg);

                void OnDelta(string chunk)
                {
                    if (string.IsNullOrEmpty(chunk)) return;
                    try
                    {
                        void Apply()
                        {
                            _assistantRaw.Append(chunk);
                            // Показываем только не кодовую часть (до/вне блоков ```)
                            var visual = StripCodeBlocks(_assistantRaw.ToString());
                            assistantMsg.Text = visual;

                            // Инкрементальное извлечение кода и построение diff
                            var codeNow = ExtractLuaCodeBlock(_assistantRaw.ToString());
                            if (!string.IsNullOrEmpty(codeNow) && !string.Equals(codeNow, _lastPreviewCode, StringComparison.Ordinal))
                            {
                                _lastPreviewCode = codeNow;
                                _lastProposedCode = codeNow.Replace("\r\n", "\n");
                                try
                                {
                                    var diff = _diff.BuildUnifiedDiffGit(currentCode, _lastProposedCode);
                                    DiffLines = diff ?? new List<string>();
                                    UpdateApplyAvailability();
                                }
                                catch { }
                            }
                        }
                        var disp = System.Windows.Application.Current?.Dispatcher;
                        if (disp == null || disp.CheckAccess()) Apply();
                        else disp.BeginInvoke(new Action(Apply));
                    }
                    catch { }
                }

                var resp = await _ai.SendAsync(prompt, currentCode, manualChangesDiff, ct, OnDelta);
                
                // Сохраняем текущий код как "последний отправленный в AI" для отслеживания будущих ручных правок
                try
                {
                    if (ParentVm != null)
                    {
                        ParentVm.LastCodeSentToAi = currentCode;
                    }
                }
                catch { }

                // После завершения запроса: обновляем отображаемый текст без кодовых блоков
                if (!string.IsNullOrEmpty(resp.AssistantText))
                {
                    var stripped = StripCodeBlocks(resp.AssistantText);
                    if (!string.Equals(assistantMsg.Text, stripped, StringComparison.Ordinal))
                        assistantMsg.Text = stripped;
                }

                // Обновление UI: добавляем краткое резюме отдельным сообщением
                if (!string.IsNullOrWhiteSpace(resp.Summary))
                {
                    Messages.Add(new AiChatMessage { Role = "assistant", Text = resp.Summary, IsSummary = true, ShowDiffButton = true });
                }

                // Итоговый diff из сервиса (на случай, если потоковый был не полным)
                if (resp.DiffLines != null && resp.DiffLines.Count > 0)
                    DiffLines = resp.DiffLines;

                _lastProposedCode = resp.ExtractedCode?.Replace("\r\n", "\n") ?? _lastProposedCode;
                UpdateApplyAvailability();

                if (BraveMode)
                {
                    TryAutoApply();
                }
            }
            catch (Exception ex)
            {
                AddAssistant("Ошибка: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                RequeryCommands();
            }
        }

        private void RequeryCommands()
        {
            try { OnPropertyChanged(nameof(CanSend)); } catch { }
            try { System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } catch { }
        }

        private void UpdateApplyAvailability()
        {
            var current = (GetCurrentCode?.Invoke() ?? string.Empty).Replace("\r\n", "\n");
            CanApply = !string.IsNullOrEmpty(_lastProposedCode) && !string.Equals(current, _lastProposedCode, StringComparison.Ordinal);
        }

        private void TryAutoApply()
        {
            if (!BraveMode) return;
            if (!CanApply) return;
            Apply();
            AddSystem("AI brave: изменения автоматически применены к редактору.");
        }

        private void Apply()
        {
            if (!CanApply) return;
            ApplyCode?.Invoke(_lastProposedCode);
            
            // Сохраняем применённый код как "последний отправленный в AI"
            try
            {
                if (ParentVm != null)
                {
                    ParentVm.LastCodeSentToAi = _lastProposedCode;
                }
            }
            catch { }
            
            UpdateApplyAvailability();
        }
    }

    public sealed class AiChatMessage : BaseViewModel
    {
        private string _role = string.Empty;
        private string _text = string.Empty;
        private bool _isSummary;
        private bool _showDiffButton;

        public string Role
        {
            get => _role;
            set { _role = value ?? string.Empty; OnPropertyChanged(); }
        }

        public string Text
        {
            get => _text;
            set { _text = value ?? string.Empty; OnPropertyChanged(); }
        }

        public bool IsSummary
        {
            get => _isSummary;
            set { _isSummary = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Показывать кнопку "Показать полные изменения" рядом с этим сообщением.
        /// </summary>
        public bool ShowDiffButton
        {
            get => _showDiffButton;
            set { _showDiffButton = value; OnPropertyChanged(); }
        }
    }
}
