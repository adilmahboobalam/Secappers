using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SecApper.FolderLocker.ViewModels;

namespace SecApper.FolderLocker.Views;

public partial class UnlockDialog : Window
{
    public Func<string, Task<string?>>? ValidatePasswordAsync { get; set; }

    public UnlockDialog()
    {
        InitializeComponent();
        Loaded += (s, e) => PwdBox.Focus();
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UnlockDialogViewModel vm)
        {
            vm.Password = PwdBox.Password;
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void PwdBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Unlock_Click(sender, e);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is UnlockDialogViewModel vm)
        {
            if (string.IsNullOrEmpty(vm.Password))
            {
                ErrorText.Text = "Please enter the folder password.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (ValidatePasswordAsync != null)
            {
                var error = await ValidatePasswordAsync(vm.Password);
                if (!string.IsNullOrEmpty(error))
                {
                    ErrorText.Text = error;
                    ErrorText.Visibility = Visibility.Visible;
                    PwdBox.SelectAll();
                    PwdBox.Focus();
                    return;
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
