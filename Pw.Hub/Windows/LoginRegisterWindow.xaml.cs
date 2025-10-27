﻿using System.Windows;
using Pw.Hub.Services;

namespace Pw.Hub.Windows;

public partial class LoginRegisterWindow : Window
{
    private readonly ModulesApiClient _api;
    private readonly bool _skipAutoLogin;

    public LoginRegisterWindow(bool skipAutoLogin = false)
    {
        InitializeComponent();
        _api = new ModulesApiClient();
        _skipAutoLogin = skipAutoLogin;
        Loaded += async (_, _) => await TryAutoLoginAsync();
    }

    private async Task TryAutoLoginAsync()
    {
        if (_skipAutoLogin) return;
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
            var resp = await _api.RegisterAsync(username, password);
            if (resp == null)
            {
                ErrorText.Text = "Регистрация не удалась";
                ErrorBorder.Visibility = Visibility.Visible;
                return;
            }
            ErrorBorder.Visibility = Visibility.Collapsed;

            // Remember me handling for registration
            var remember = RememberCheck.IsChecked == true;
            AuthState.Set(_api.Token, _api.CurrentUser, remember);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorBorder.Visibility = Visibility.Visible;
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
                ErrorBorder.Visibility = Visibility.Visible;
                return;
            }
            ErrorBorder.Visibility = Visibility.Collapsed;

            // Remember me handling for login
            var remember = RememberCheck.IsChecked == true;
            AuthState.Set(_api.Token, _api.CurrentUser, remember);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorBorder.Visibility = Visibility.Visible;
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
