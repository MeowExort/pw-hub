using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Services;

namespace Pw.Hub.ViewModels;

/// <summary>
/// Главная ViewModel приложения. Содержит состояние навигационного дерева, команды верхнего меню
/// и управление попапом обновлений. Вся бизнес-логика здесь изолирована от представления (MVVM).
/// </summary>
public class MainViewModel : BaseViewModel
{
    /// <summary>
    /// Коллекция модулей для блока «Быстрый запуск» в левом меню.
    /// </summary>
    public ObservableCollection<ModuleDefinition> QuickModules { get; } = new();

    private ModuleDefinition _selectedQuickModule;
    /// <summary>
    /// Текущий выбранный модуль для быстрого запуска.
    /// </summary>
    public ModuleDefinition SelectedQuickModule
    {
        get => _selectedQuickModule;
        set { _selectedQuickModule = value; OnPropertyChanged(); }
    }
    private readonly IWindowService _windowService;
    private readonly IUpdatesCheckService _updatesCheckService;
    private readonly IRunModuleCoordinator _runCoordinator;
    private readonly IUiDialogService _dialogs;
    private readonly IAccountsService _accounts;
    private readonly ModuleService _moduleService = new();
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private List<string> _lastUpdates = new();

    /// <summary>
    /// Коллекция отрядов (с аккаунтами) для левой панели навигации.
    /// ObservableCollection используется для автоматического обновления UI при изменениях.
    /// </summary>
    public ObservableCollection<Squad> Squads { get; } = new();

    private bool _isUpdatesPopupOpen;
    /// <summary>
    /// Признак видимости попапа с уведомлением об обновлениях модулей.
    /// Управляется через биндинг из XAML, без прямых обращений из окна.
    /// </summary>
    public bool IsUpdatesPopupOpen
    {
        get => _isUpdatesPopupOpen;
        set { _isUpdatesPopupOpen = value; OnPropertyChanged(); }
    }

    private string _updatesText = string.Empty;
    /// <summary>
    /// Текст внутри попапа обновлений (список модулей и т.п.).
    /// </summary>
    public string UpdatesText
    {
        get => _updatesText;
        set { _updatesText = value; OnPropertyChanged(); }
    }

    // Commands
    /// <summary>
    /// Команда открытия окна библиотеки модулей (немодально).
    /// </summary>
    public ICommand OpenModulesLibraryCommand { get; }
    /// <summary>
    /// Команда открытия окна редактирования профиля (модально).
    /// </summary>
    public ICommand OpenProfileCommand { get; }
    /// <summary>
    /// Команда закрытия попапа обновлений.
    /// </summary>
    public ICommand CloseUpdatesPopupCommand { get; }
    /// <summary>
    /// Команда быстрого запуска выбранного модуля.
    /// </summary>
    public ICommand RunSelectedModuleCommand { get; }
    /// <summary>
    /// Команда загрузки персонажей для выбранного аккаунта (из контекстного меню дерева).
    /// </summary>
    public ICommand LoadCharactersCommand { get; }

    /// <summary>
    /// Команда добавления нового отряда (контекстное меню и пустое состояние дерева).
    /// </summary>
    public ICommand AddSquadCommand { get; }

    /// <summary>
    /// Команда добавления аккаунта в выбранный отряд.
    /// Принимает параметр: Squad или Account (в последнем случае будет использован родительский отряд аккаунта).
    /// </summary>
    public ICommand AddAccountCommand { get; }

    /// <summary>
    /// Команда редактирования выбранного отряда. Параметр: Squad.
    /// </summary>
    public ICommand EditSquadCommand { get; }

    /// <summary>
    /// Команда редактирования выбранного аккаунта. Параметр: Account.
    /// </summary>
    public ICommand EditAccountCommand { get; }

    /// <summary>
    /// Команда удаления выбранного узла дерева (отряд или аккаунт). Параметр: Squad или Account.
    /// </summary>
    public ICommand DeleteNodeCommand { get; }

    /// <summary>
    /// Конструктор с внедрением зависимостей.
    /// </summary>
    /// <param name="windowService">Сервис открытия окон.</param>
    public MainViewModel(IWindowService windowService, IUpdatesCheckService updatesCheckService, IRunModuleCoordinator runCoordinator, IUiDialogService dialogs, IAccountsService accounts)
    {
        _windowService = windowService;
        _updatesCheckService = updatesCheckService;
        _runCoordinator = runCoordinator;
        _dialogs = dialogs ?? new UiDialogService();
        _accounts = accounts ?? new AccountsService();
        // init commands
        OpenModulesLibraryCommand = new RelayCommand(_ => OpenModulesLibrary());
        OpenProfileCommand = new RelayCommand(_ => OpenProfile());
        CloseUpdatesPopupCommand = new RelayCommand(_ => IsUpdatesPopupOpen = false);
        RunSelectedModuleCommand = new RelayCommand(async _ => await RunSelectedModuleAsync(), _ => SelectedQuickModule != null);
        LoadCharactersCommand = new RelayCommand(p => OnLoadCharactersRequested(p), p => p is Models.Account);
        AddSquadCommand = new RelayCommand(_ => AddSquad());
        AddAccountCommand = new RelayCommand(p => AddAccount(p), p => p is Models.Squad || p is Models.Account);
        EditSquadCommand = new RelayCommand(p => EditSquad(p as Models.Squad), p => p is Models.Squad);
        EditAccountCommand = new RelayCommand(p => EditAccount(p as Models.Account), p => p is Models.Account);
        DeleteNodeCommand = new RelayCommand(p => DeleteNode(p), p => p is Models.Squad || p is Models.Account);

        LoadData();
        LoadQuickModules();
    }

    /// <summary>
    /// Добавляет аккаунт в указанный отряд. Параметр может быть Squad или Account (в этом случае используется родительский отряд).
    /// Открывает диалог создания аккаунта и сохраняет его в БД с корректным порядком (OrderIndex).
    /// </summary>
    private async void AddAccount(object parameter)
    {
        try
        {
            // Определяем целевой отряд
            Squad targetSquad = null;
            if (parameter is Squad s)
            {
                targetSquad = s;
            }
            else if (parameter is Account acc)
            {
                targetSquad = Squads.FirstOrDefault(q => q.Accounts.Any(a => a.Id == acc.Id));
            }
            if (targetSquad == null) return;

            // Диалог ввода имени аккаунта
            var dlg = new Pw.Hub.Pages.CreateAccountWindow();
            var ok = _windowService.ShowDialog(dlg);
            if (ok == true)
            {
                // Создаём через сервис и получаем созданный аккаунт
                var created = await _accounts.CreateAccountAsync(targetSquad, dlg.AccountName);
                Reload();
                try { RequestSelectTreeItem?.Invoke(created); } catch { }
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Редактирует выбранный отряд: открывает диалог, сохраняет имя в БД и обновляет UI.
    /// </summary>
    private void EditSquad(Squad squad)
    {
        if (squad == null) return;
        try
        {
            var dlg = new Pw.Hub.Pages.EditSquadWindow(squad);
            var ok = _windowService.ShowDialog(dlg);
            if (ok == true)
            {
                // Сохранение через сервис аккаунтов
                _ = _accounts.UpdateSquadAsync(squad, dlg.SquadName);
                Reload();
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Редактирует выбранный аккаунт: открывает диалог, сохраняет имя и обновляет UI.
    /// </summary>
    private void EditAccount(Account account)
    {
        if (account == null) return;
        try
        {
            var dlg = new Pw.Hub.Pages.EditAccountWindow(account);
            var ok = _windowService.ShowDialog(dlg);
            if (ok == true)
            {
                _ = _accounts.UpdateAccountAsync(account, dlg.AccountName);
                Reload();
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Удаляет выбранный узел дерева (отряд или аккаунт) с подтверждением и обновлением UI.
    /// </summary>
    private void DeleteNode(object parameter)
    {
        try
        {
            if (parameter is Squad squad)
            {
                if (!_dialogs.Confirm($"Вы действительно хотите удалить отряд \"{squad.Name}\"?\nВсе аккаунты в этом отряде также будут удалены.", "Подтверждение удаления"))
                    return;
                _ = _accounts.DeleteSquadAsync(squad);
                Reload();
            }
            else if (parameter is Account acc)
            {
                if (!_dialogs.Confirm($"Вы действительно хотите удалить аккаунт \"{acc.Name}\"?", "Подтверждение удаления"))
                    return;
                _ = _accounts.DeleteAccountAsync(acc);
                Reload();
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Конструктор по умолчанию для дизайнеров/XAML — создаёт WindowService по умолчанию.
    /// В рабочем приложении используется конструктор с DI.
    /// </summary>
    public MainViewModel() : this(new WindowService(), new UpdatesCheckService(), new RunModuleCoordinator(new WindowService(), new LuaExecutionService()), new UiDialogService(), new AccountsService())
    {
    }

    /// <summary>
    /// Загружает список локальных модулей для блока «Быстрый запуск».
    /// </summary>
    public void ReloadQuickModules() => LoadQuickModules();

        private void LoadQuickModules()
    {
        try
        {
            var list = _moduleService.LoadModules() ?? new List<ModuleDefinition>();
            QuickModules.Clear();
            foreach (var m in list)
                QuickModules.Add(m);
            if (SelectedQuickModule == null)
                SelectedQuickModule = QuickModules.FirstOrDefault();
        }
        catch
        {
            // ignore load errors
        }
        finally
        {
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Выполняет сценарий быстрого запуска выбранного модуля: собирает аргументы, сохраняет LastArgs
    /// и инициирует запуск через событие для окна.
    /// </summary>
    private async Task RunSelectedModuleAsync()
    {
        var module = SelectedQuickModule;
        if (module == null) return;
        try
        {
            await _runCoordinator.RunAsync(module);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Обработчик команды загрузки персонажей: пробрасывает запрос вверх через событие,
    /// чтобы View выполнила фактическую операцию через сервис.
    /// </summary>
    /// <param name="parameter">Ожидается объект типа Account.</param>
    private void OnLoadCharactersRequested(object parameter)
    {
        if (parameter is Models.Account acc)
        {
            try { RequestLoadCharacters?.Invoke(acc); } catch { }
        }
    }


    /// <summary>
    /// Событие-запрос на загрузку персонажей для переданного аккаунта.
    /// Окно подписывается и делегирует выполнение сервису загрузки.
    /// </summary>
    public event Action<Models.Account> RequestLoadCharacters;

    /// <summary>
    /// Событие-запрос на выбор элемента в дереве навигации (отряд или аккаунт).
    /// Вызывается после создания отряда/аккаунта, чтобы View выделила его в TreeView.
    /// </summary>
    public event Action<object> RequestSelectTreeItem;

    /// <summary>
    /// Открывает окно библиотеки модулей (немодально). Одновременно скрывает попап обновлений.
    /// </summary>
    private void OpenModulesLibrary()
    {
        try
        {
            var win = new Pw.Hub.Windows.ModulesLibraryWindow();
            _windowService.ShowWindow(win);
            // Hide popup if it was open
            IsUpdatesPopupOpen = false;
        }
        catch { }
    }

    /// <summary>
    /// Открывает модальное окно редактирования профиля пользователя.
    /// </summary>
    private void OpenProfile()
    {
        try
        {
            var win = new Pw.Hub.Windows.ProfileEditWindow();
            _windowService.ShowDialog(win);
        }
        catch { }
    }

    /// <summary>
    /// Загружает данные из БД: отряды с аккаунтами и связанными сущностями.
    /// Обеспечивает сортировку и преобразование в ObservableCollection для корректного биндинга.
    /// </summary>
    private void LoadData()
    {
        // Загрузка через сервис аккаунтов
        var squads = _accounts.LoadSquads();
        Squads.Clear();
        foreach (var s in squads)
            Squads.Add(s);
    }

    /// <summary>
    /// Полная перезагрузка данных для обновления UI после изменений.
    /// </summary>
    public void Reload()
    {
        LoadData();
    }

    /// <summary>
    /// Добавляет новый отряд через диалог и сохраняет его в БД; затем перезагружает список.
    /// </summary>
    private async void AddSquad()
    {
        try
        {
            var dlg = new Pw.Hub.Pages.CreateSquadWindow();
            var ok = _windowService.ShowDialog(dlg);
            if (ok == true)
            {
                var created = await _accounts.CreateSquadAsync(dlg.SquadName);
                Reload();
                try { RequestSelectTreeItem?.Invoke(created); } catch { }
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Проверяет наличие обновлений модулей c защитой от частых запросов (дебаунс 60 сек.).
    /// При наличии обновлений формирует текст и открывает попап; иначе скрывает его.
    /// </summary>
    /// <param name="force">Если true — принудительно выполняет проверку, игнорируя дебаунс.</param>
    public async Task CheckUpdatesDebouncedAsync(bool force = false)
    {
        try
        {
            if (!force && (DateTime.UtcNow - _lastUpdateCheck) < TimeSpan.FromSeconds(60))
                return;
            _lastUpdateCheck = DateTime.UtcNow;

            var updates = await _updatesCheckService.GetUpdatesAsync();
            if (updates == null) updates = new List<string>();

            // Не пересказывать пользователю одно и то же
            if (_lastUpdates.SequenceEqual(updates))
                return;
            _lastUpdates = updates;

            if (updates.Count > 0)
            {
                var list = string.Join(Environment.NewLine, updates.Take(5));
                if (updates.Count > 5) list += $" и ещё {updates.Count - 5}";
                UpdatesText = $"Доступны обновления для модулей: {Environment.NewLine} {list}";
                IsUpdatesPopupOpen = true;
            }
            else
            {
                IsUpdatesPopupOpen = false;
            }
        }
        catch
        {
            // Не шумим пользователю об ошибках сетевого запроса/парсинга
            IsUpdatesPopupOpen = false;
        }
    }
}
