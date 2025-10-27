using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Pw.Hub.Services;

namespace Pw.Hub.Windows;

public partial class ProfileEditWindow : Window
{
    private readonly ModulesApiClient _api;

    public ProfileEditWindow()
    {
        InitializeComponent();
        _api = new ModulesApiClient();
        Loaded += async (_, _) => await LoadUser();
    }

    private void SwitchAccountBtn_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new LoginRegisterWindow(skipAutoLogin: true)
            {
                Owner = this
            };
            var result = dlg.ShowDialog();
            if (result == true)
            {
                // After successful login, close this dialog so the app continues under the new account
                DialogResult = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка: " + ex.Message, "Смена аккаунта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadUser()
    {
        try
        {
            var me = await _api.MeAsync();
            if (me != null)
            {
                UsernameBox.Text = me.Username ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка загрузки профиля: " + ex.Message, "Профиль", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveUsernameBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var name = (UsernameBox.Text ?? string.Empty).Trim();
        if (name.Length < 3)
        {
            MessageBox.Show("Имя пользователя должно содержать не менее 3 символов", "Профиль", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var res = await _api.UpdateUsernameAsync(name);
            if (res == null)
            {
                MessageBox.Show("Не удалось сохранить имя. Возможно, имя занято.", "Профиль", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MessageBox.Show("Имя пользователя обновлено", "Профиль", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка: " + ex.Message, "Профиль", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ChangePasswordBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var current = CurrentPasswordBox.Password ?? string.Empty;
        var next = NewPasswordBox.Password ?? string.Empty;
        var confirm = ConfirmPasswordBox.Password ?? string.Empty;
        if (next.Length < 3)
        {
            MessageBox.Show("Новый пароль слишком короткий", "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!string.Equals(next, confirm, StringComparison.Ordinal))
        {
            MessageBox.Show("Пароли не совпадают", "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var ok = await _api.ChangePasswordAsync(current, next);
            if (!ok)
            {
                MessageBox.Show("Не удалось изменить пароль. Проверьте текущий пароль.", "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            CurrentPasswordBox.Password = string.Empty;
            NewPasswordBox.Password = string.Empty;
            ConfirmPasswordBox.Password = string.Empty;
            MessageBox.Show("Пароль изменён", "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка: " + ex.Message, "Смена пароля", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
