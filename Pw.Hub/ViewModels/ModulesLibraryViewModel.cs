using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Markdig;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Services;

namespace Pw.Hub.ViewModels;

/// <summary>
/// ViewModel для окна библиотеки модулей. Инкапсулирует загрузку/поиск модулей,
/// вычисление доступности обновлений и подготовку HTML-описания для просмотра.
/// Вся бизнес-логика вынесена из окна, что упрощает тестирование и сопровождение.
/// </summary>
public class ModulesLibraryViewModel : INotifyPropertyChanged
{
    private readonly IWindowService _windowService;
    private readonly ModulesApiClient _api;
    private readonly ModuleService _moduleService;
    private readonly IModulesSyncService _modulesSync;

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Коллекция модулей для отображения в списке.
    /// </summary>
    public ObservableCollection<ModuleDto> Modules { get; } = new();

        /// <summary>
        /// Команды панели разработчика.
        /// </summary>
        public ICommand CreateModuleCommand { get; private set; }
        public ICommand EditModuleCommand { get; private set; }
        public ICommand DeleteModuleCommand { get; private set; }
        public ICommand OpenLuaEditorCommand { get; private set; }

    private ModuleDto _selectedModule;
    /// <summary>
    /// Текущий выбранный модуль в списке.
    /// </summary>
    public ModuleDto SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (_selectedModule == value) return;
            _selectedModule = value;
            OnPropertyChanged();
            UpdateDetailsForSelection();
        }
    }

    private string _searchText;
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); }
    }

    private string _tagsText;
    public string TagsText
    {
        get => _tagsText;
        set { _tagsText = value; OnPropertyChanged(); }
    }

    private string _sortOption = "name";
    /// <summary>
    /// Ключ сортировки (name, installs, runs, popular).
    /// </summary>
    public string SortOption
    {
        get => _sortOption;
        set { _sortOption = value; OnPropertyChanged(); }
    }

    private string _orderOption = "desc";
    /// <summary>
    /// Порядок сортировки (asc/desc).
    /// </summary>
    public string OrderOption
    {
        get => _orderOption;
        set { _orderOption = value; OnPropertyChanged(); }
    }

    private string _titleText = string.Empty;
    /// <summary>
    /// Заголовок карточки выбранного модуля.
    /// </summary>
    public string TitleText
    {
        get => _titleText;
        set { _titleText = value; OnPropertyChanged(); }
    }

    private string _descriptionHtml = string.Empty;
    /// <summary>
    /// HTML-описание выбранного модуля (рендерится из Markdown). Для показа в WebView2.
    /// </summary>
    public string DescriptionHtml
    {
        get => _descriptionHtml;
        set { _descriptionHtml = value; OnPropertyChanged(); }
    }

    private bool _isDeveloper;
    /// <summary>
    /// Признак режима разработчика для текущего пользователя (панель разработчика в UI).
    /// </summary>
    public bool IsDeveloper
    {
        get => _isDeveloper;
        set { _isDeveloper = value; OnPropertyChanged(); }
    }

    private bool _canUpdateAll;
    /// <summary>
    /// Признак, что есть хотя бы один модуль с доступным обновлением.
    /// </summary>
    public bool CanUpdateAll
    {
        get => _canUpdateAll;
        set { _canUpdateAll = value; OnPropertyChanged(); }
    }

    private bool _canUpdateSelected;
    /// <summary>
    /// Признак, что для выбранного модуля доступно обновление.
    /// </summary>
    public bool CanUpdateSelected
    {
        get => _canUpdateSelected;
        set { _canUpdateSelected = value; OnPropertyChanged(); }
    }

    // Команды UI
    public ICommand SearchCommand { get; }
    public ICommand UpdateAllCommand { get; }
    public ICommand UpdateSelectedCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }

    /// <summary>
    /// Конструктор по умолчанию. Создаёт необходимые сервисы.
    /// </summary>
    public ModulesLibraryViewModel() : this(new WindowService(), new ModulesSyncService())
    {
    }

    /// <summary>
    /// Конструктор с внедрением зависимостей через IWindowService.
    /// </summary>
    public ModulesLibraryViewModel(IWindowService windowService, IModulesSyncService modulesSyncService)
    {
        _windowService = windowService ?? new WindowService();
        _api = new ModulesApiClient();
        _moduleService = new ModuleService();
        _modulesSync = modulesSyncService ?? new ModulesSyncService();

        SearchCommand = new RelayCommand(async _ => await SearchAsync());
        UpdateAllCommand = new RelayCommand(async _ => await UpdateAllAsync(), _ => CanUpdateAll);
        UpdateSelectedCommand = new RelayCommand(async _ => await UpdateSelectedAsync(), _ => CanUpdateSelected);
        InstallCommand = new RelayCommand(async _ => await InstallSelectedAsync(), _ => SelectedModule != null);
        UninstallCommand = new RelayCommand(_ => UninstallSelected(), _ => SelectedModule != null);

        // Dev-команды
        CreateModuleCommand = new RelayCommand(_ => CreateModule(), _ => IsDeveloper);
        EditModuleCommand = new RelayCommand(_ => EditModule(), _ => IsDeveloper && SelectedModule != null && IsOwnedByCurrentUser(SelectedModule));
        DeleteModuleCommand = new RelayCommand(_ => DeleteModule(), _ => IsDeveloper && SelectedModule != null && IsOwnedByCurrentUser(SelectedModule));
        OpenLuaEditorCommand = new RelayCommand(_ => OpenLuaEditor());
    }

    /// <summary>
    /// Первичная инициализация: проверка пользователя/режима разработчика и начальный поиск.
    /// </summary>
    public async Task InitAsync()
    {
        try
        {
            if (_api.CurrentUser == null)
                await _api.MeAsync();
            IsDeveloper = _api.CurrentUser?.Developer == true;
        }
        catch { IsDeveloper = false; }

        await SearchAsync();
    }

    /// <summary>
    /// Выполняет поиск модулей по текущим параметрам и обновляет список.
    /// </summary>
    public async Task SearchAsync()
    {
        try
        {
            var resp = await _api.SearchAsync(SearchText, TagsText, SortOption, OrderOption, 1, 50);
            Modules.Clear();
            foreach (var m in resp.Items)
                Modules.Add(m);

            // Пересчёт индикаторов обновления
            RecomputeUpdateIndicators();

            if (Modules.Count > 0)
                SelectedModule = Modules[0];
        }
        catch
        {
            // В случае ошибки просто оставляем текущий список как есть
        }
    }

    /// <summary>
    /// Выполняет обновление всех модулей, для которых оно доступно.
    /// </summary>
    private async Task UpdateAllAsync()
    {
        foreach (var m in Modules.ToList())
        {
            if (IsUpdateAvailable(GetLocalVersion(m.Id), m.Version ?? "1.0.0"))
            {
                await InstallModuleAsync(m);
            }
        }
        // После установки пересчитываем индикаторы
        RecomputeUpdateIndicators();
        UpdateSelectedFlags();
    }

    /// <summary>
    /// Устанавливает/обновляет выбранный модуль, если он выбран.
    /// </summary>
    private async Task UpdateSelectedAsync()
    {
        if (SelectedModule == null) return;
        await InstallModuleAsync(SelectedModule);
        RecomputeUpdateIndicators();
        UpdateSelectedFlags();
    }

    /// <summary>
    /// Устанавливает текущий выбранный модуль, если выбран.
    /// </summary>
    private async Task InstallSelectedAsync()
    {
        if (SelectedModule == null) return;
        await InstallModuleAsync(SelectedModule);
        RecomputeUpdateIndicators();
        UpdateSelectedFlags();
    }

    /// <summary>
    /// Удаляет выбранный модуль локально.
    /// </summary>
    private void UninstallSelected()
    {
        if (SelectedModule == null) return;
        _modulesSync.RemoveModuleLocally(SelectedModule.Id);
        RecomputeUpdateIndicators();
        UpdateSelectedFlags();
    }

    // Вспомогательные методы -----------------------------------------------

    /// <summary>
    /// Обновляет заголовок/описание и доступность команд при смене выбора.
    /// </summary>
    private void UpdateDetailsForSelection()
    {
        var m = SelectedModule;
        TitleText = m?.Name ?? string.Empty;
        // Рендерим Markdown в HTML для WebView2
        var md = m?.Description ?? string.Empty;
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var html = Markdown.ToHtml(md, pipeline);
        DescriptionHtml = "<html><head><meta charset='utf-8'></head><body style='color:#CCC;background:#1E1E1E;font-family:Segoe UI'>" + html + "</body></html>";

        UpdateSelectedFlags();
    }

    /// <summary>
    /// Пересчитывает доступность обновлений для списка и выставляет CanUpdateAll.
    /// </summary>
    private void RecomputeUpdateIndicators()
    {
        try
        {
            var any = Modules.Any(m => IsUpdateAvailable(GetLocalVersion(m.Id), m.Version ?? "1.0.0"));
            CanUpdateAll = any;
        }
        catch { CanUpdateAll = false; }
    }

    /// <summary>
    /// Обновляет флаг доступности обновления для выбранного модуля.
    /// </summary>
    private void UpdateSelectedFlags()
    {
        var m = SelectedModule;
        if (m == null) { CanUpdateSelected = false; return; }
        CanUpdateSelected = IsUpdateAvailable(GetLocalVersion(m.Id), m.Version ?? "1.0.0");
        // Пересчёт доступности dev-команд, зависящих от SelectedModule
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Устанавливает или обновляет модуль локально по данным, полученным с сервера.
    /// Сохраняет файл скрипта и обновляет запись в локальном каталоге модулей.
    /// </summary>
    private async Task InstallModuleAsync(ModuleDto module)
    {
        try
        {
            // Получаем полные данные модуля (включая Script)
            var item = await _api.GetModuleAsync(module.Id);
            if (item != null)
                _modulesSync.InstallModuleLocally(item);
        }
        catch
        {
            // игнорируем ошибки установки на уровне VM; UI может показать общий статус
        }
    }

    // ===== Dev операции (создание/редактирование/удаление/редактор Lua) =====

    /// <summary>
    /// Проверка, что выбранный модуль принадлежит текущему пользователю (для прав на редактирование/удаление).
    /// </summary>
    private bool IsOwnedByCurrentUser(ModuleDto m)
    {
        try
        {
            var uid = _api.CurrentUser?.UserId;
            if (string.IsNullOrWhiteSpace(uid) || m == null) return false;
            return string.Equals(m.OwnerUserId, uid, StringComparison.Ordinal);
        }
        catch { return false; }
    }

    /// <summary>
    /// Открыть окно создания модуля через API-редактор. После сохранения — обновить список и выделение.
    /// </summary>
    private async void CreateModule()
    {
        try
        {
            var editor = new Pw.Hub.Windows.ModulesApiEditorWindow();
            _windowService.ShowWindow(editor);

            // Подписываемся на закрытие, чтобы обработать сохранение
            editor.Closed += async (_, __) =>
            {
                try
                {
                    if (editor.IsSaved)
                    {
                        var req = editor.GetRequest();
                        var created = await _api.CreateModuleAsync(req);
                        if (created != null)
                        {
                            await SearchAsync();
                            SelectedModule = Modules.FirstOrDefault(x => x.Id == created.Id) ?? created;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось создать модуль: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка открытия редактора: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Открыть окно редактирования выбранного модуля для владельца; по сохранению — обновить список.
    /// </summary>
    private async void EditModule()
    {
        var m = SelectedModule;
        if (m == null)
        {
            MessageBox.Show("Выберите модуль");
            return;
        }
        if (!IsOwnedByCurrentUser(m) || _api.CurrentUser?.Developer != true)
        {
            MessageBox.Show("Можно редактировать только свои модули");
            return;
        }

        try
        {
            var editor = new Pw.Hub.Windows.ModulesApiEditorWindow(m);
            _windowService.ShowWindow(editor);
            editor.Closed += async (_, __) =>
            {
                try
                {
                    if (editor.IsSaved)
                    {
                        var req = editor.GetRequest();
                        var updated = await _api.UpdateModuleAsync(m.Id, req);
                        if (updated != null)
                        {
                            await SearchAsync();
                            SelectedModule = Modules.FirstOrDefault(x => x.Id == updated.Id) ?? updated;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось обновить модуль: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };            
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка открытия редактора: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Удаление выбранного собственного модуля с подтверждением; по успеху — обновить список.
    /// </summary>
    private async void DeleteModule()
    {
        var m = SelectedModule;
        if (m == null) return;
        if (!IsOwnedByCurrentUser(m) || _api.CurrentUser?.Developer != true)
        {
            MessageBox.Show("Можно удалять только свои модули");
            return;
        }
        if (MessageBox.Show("Удалить модуль?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            if (await _api.DeleteModuleAsync(m.Id))
            {
                await SearchAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось удалить модуль: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Открытие редактора Lua. Показывается немодально, чтобы не блокировать MainWindow и API.
    /// </summary>
    private void OpenLuaEditor()
    {
        try
        {
            // Пытаемся назначить владельца как MainWindow
            var owner = Application.Current?.MainWindow as Window;
            var win = new Pw.Hub.Windows.LuaEditorWindow((owner as Pw.Hub.MainWindow)?.AccountPage?.LuaRunner)
            {
                Owner = owner
            };
            _windowService.ShowWindow(win);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка открытия редактора Lua: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }



    /// <summary>
    /// Возвращает локальную версию модуля по Id ("1.0.0" по умолчанию, если нет данных).
    /// </summary>
    private string GetLocalVersion(Guid id)
    {
        try
        {
            var locals = _moduleService.LoadModules();
            var m = locals.FirstOrDefault(x => string.Equals(x.Id, id.ToString(), StringComparison.OrdinalIgnoreCase));
            return m?.Version ?? "1.0.0";
        }
        catch { return "1.0.0"; }
    }

    /// <summary>
    /// Сравнивает версии и определяет, доступно ли обновление (server > local).
    /// </summary>
    private static bool IsUpdateAvailable(string local, string server)
    {
        try
        {
            var lv = ParseVersion(local);
            var rv = ParseVersion(server);
            for (int i = 0; i < Math.Max(lv.Length, rv.Length); i++)
            {
                var a = i < lv.Length ? lv[i] : 0;
                var b = i < rv.Length ? rv[i] : 0;
                if (a != b) return a < b;
            }
            return false;
        }
        catch
        {
            return !string.Equals(local, server, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int[] ParseVersion(string v)
    {
        return (v ?? "0").Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .ToArray();
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
