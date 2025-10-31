using System.Windows;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows;

/// <summary>
/// Окно настроек API-ключа. Вся бизнес-логика вынесена во ViewModel.
/// Code-behind отвечает только за инициализацию и реакцию на события VM (закрытие, ошибки).
/// </summary>
public partial class ApiKeySettingsWindow : Window
{
    public ApiKeySettingsViewModel Vm { get; }

    public string ApiKey => Vm.ApiKey;

    public ApiKeySettingsWindow(string currentApiKey)
    {
        InitializeComponent();
        Vm = new ApiKeySettingsViewModel { ApiKey = currentApiKey ?? string.Empty };
        Vm.RequestClose += OnRequestClose;
        DataContext = Vm;
    }

    private void OnRequestClose(bool? dialogResult)
    {
        try
        {
            DialogResult = dialogResult;
        }
        catch { }
        Close();
    }
}