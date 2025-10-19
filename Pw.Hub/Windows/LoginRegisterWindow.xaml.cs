using System;
using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Services;

namespace Pw.Hub.Windows;

public partial class LoginRegisterWindow : Window
{
    private readonly ModulesApiClient _api;

    public LoginRegisterWindow()
    {
        InitializeComponent();
        _api = new ModulesApiClient();
        Loaded += async (_, _) => await TryAutoLoginAsync();
    }

    private async Task TryAutoLoginAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(AuthState.Token))
            {
                var me = await _api.MeAsync();
                if (me != null)
                {
                    DialogResult = true;
                    Close();
                    return;
                }
            }
        }
        catch { }
    }

    private async void RegisterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var username = UsernameText.Text?.Trim() ?? string.Empty;
            var password = PasswordBox.Password ?? string.Empty;
            var dev = DeveloperCheck.IsChecked == true;
            var resp = await _api.RegisterAsync(username, password, dev);
            if (resp == null)
            {
                ErrorText.Text = "Регистрация не удалась";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
            ErrorText.Visibility = Visibility.Collapsed;

            // Remember me handling for registration
            var remember = RememberCheck.IsChecked == true;
            if (remember)
            {
                AuthState.Set(_api.Token, _api.CurrentUser);
            }
            else
            {
                AuthState.Set(null, null);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void LoginClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var username = UsernameText.Text?.Trim() ?? string.Empty;
            var password = PasswordBox.Password ?? string.Empty;
            var resp = await _api.LoginAsync(username, password);
            if (resp == null)
            {
                ErrorText.Text = "Вход не удался";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
            ErrorText.Visibility = Visibility.Collapsed;

            // Remember me handling for login
            var remember = RememberCheck.IsChecked == true;
            if (remember)
            {
                AuthState.Set(_api.Token, _api.CurrentUser);
            }
            else
            {
                AuthState.Set(null, null);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
