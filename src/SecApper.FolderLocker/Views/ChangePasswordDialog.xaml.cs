using System.Windows;
using SecApper.FolderLocker.ViewModels;

namespace SecApper.FolderLocker.Views;

public partial class ChangePasswordDialog : Window
{
    public ChangePasswordDialog()
    {
        InitializeComponent();
        Loaded += (s, e) => CurrentBox.Focus();
    }

    private void CurrentBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordDialogViewModel vm)
            vm.CurrentPassword = CurrentBox.Password;
    }

    private void NewBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordDialogViewModel vm)
            vm.NewPassword = NewBox.Password;
    }

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordDialogViewModel vm)
            vm.ConfirmNewPassword = ConfirmBox.Password;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordDialogViewModel vm)
        {
            if (string.IsNullOrEmpty(vm.CurrentPassword))
            {
                ErrorText.Text = "Please enter your current password.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (string.IsNullOrEmpty(vm.NewPassword) || vm.NewPassword.Length < 6)
            {
                ErrorText.Text = "New password must be at least 6 characters.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (vm.NewPassword != vm.ConfirmNewPassword)
            {
                ErrorText.Text = "New passwords do not match.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
