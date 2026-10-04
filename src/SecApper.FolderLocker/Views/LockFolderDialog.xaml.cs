using System.Windows;
using SecApper.FolderLocker.ViewModels;

namespace SecApper.FolderLocker.Views;

public partial class LockFolderDialog : Window
{
    public LockFolderDialog()
    {
        InitializeComponent();
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LockFolderDialogViewModel vm)
        {
            vm.Password = PwdBox.Password;
        }
    }

    private void ConfirmPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LockFolderDialogViewModel vm)
        {
            vm.ConfirmPassword = ConfirmPwdBox.Password;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LockFolderDialogViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Password))
            {
                ShowError("Password cannot be empty.");
                return;
            }

            if (vm.Password.Length < 6)
            {
                ShowError("Password must be at least 6 characters.");
                return;
            }

            if (vm.Password != vm.ConfirmPassword)
            {
                ShowError("Passwords do not match.");
                return;
            }

            DialogResult = true;
            Close();
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
