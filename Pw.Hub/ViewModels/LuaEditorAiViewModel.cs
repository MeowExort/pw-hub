using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Pw.Hub.Infrastructure;
using Pw.Hub.Services;

namespace Pw.Hub.ViewModels
{
    /// <summary>
    /// Под-VM для AI-панели редактора Lua. Управляет сообщениями чата, отправкой запросов,
    /// предпросмотром diff и применением изменений к редактору.
    /// </summary>
    public class LuaEditorAiViewModel : BaseViewModel
    {
        private readonly IAiAssistantService _ai;

        public LuaEditorAiViewModel()
        {
            // Разрешаем сервис через DI с безопасным fallback
            _ai = (App.Services?.GetService(typeof(IAiAssistantService)) as IAiAssistantService)
                  ?? new AiAssistantService(new DiffPreviewService());

            SendCommand = new RelayCommand(async _ => await SendAsync(), _ => CanSend);
            NewSessionCommand = new RelayCommand(_ => NewSession(), _ => !IsBusy);
            ApplyCommand = new RelayCommand(_ => Apply(), _ => CanApply);
        }

        // Делегаты связи с основным VM редактора
        public Func<string> GetCurrentCode { get; set; } = () => string.Empty;
        public Action<string> ApplyCode { get; set; } = _ => { };

        public ObservableCollection<AiChatMessage> Messages { get; } = new();

        private IList<string> _diffLines = new List<string>();
        public IList<string> DiffLines { get => _diffLines; private set { _diffLines = value ?? new List<string>(); OnPropertyChanged(); UpdateApplyAvailability(); } }

        private string _inputText = string.Empty;
        public string InputText { get => _inputText; set { _inputText = value ?? string.Empty; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private bool _braveMode;
        public bool BraveMode { get => _braveMode; set { _braveMode = value; OnPropertyChanged(); if (_braveMode) TryAutoApply(); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private bool _canApply;
        public bool CanApply { get => _canApply; private set { _canApply = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        private string _lastProposedCode = string.Empty;

        public ICommand SendCommand { get; }
        public ICommand NewSessionCommand { get; }
        public ICommand ApplyCommand { get; }

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
            _ai.NewSession();
            AddSystem("Новая сессия начата. Опишите задачу для AI.");
        }

        private async Task SendAsync(CancellationToken ct = default)
        {
            if (!CanSend) return;
            IsBusy = true;
            try
            {
                var prompt = InputText.Trim();
                AddUser(prompt);
                InputText = string.Empty;

                var currentCode = (GetCurrentCode?.Invoke() ?? string.Empty).Replace("\r\n", "\n");
                var resp = await _ai.SendAsync(prompt, currentCode, ct).ConfigureAwait(false);

                // Обновление UI
                AddAssistant(resp.AssistantText);
                DiffLines = resp.DiffLines ?? new List<string>();

                _lastProposedCode = resp.ExtractedCode?.Replace("\r\n", "\n") ?? string.Empty;
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
            }
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
            UpdateApplyAvailability();
        }
    }

    public sealed class AiChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
