using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using SecApper.FolderLocker.ViewModels;

namespace SecApper.FolderLocker.Views;

public partial class UnlockDialog : Window
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public Func<string, Task<string?>>? ValidatePasswordAsync { get; set; }
    private bool _isPasswordVisible;

    public UnlockDialog()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            try
            {
                Topmost = true;
                Activate();
                Focus();
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    SetForegroundWindow(helper.Handle);
                }
            }
            catch
            {
            }
            PwdBox.Focus();
            Keyboard.Focus(PwdBox);
        };
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        if (_isPasswordVisible)
        {
            TxtBox.Text = PwdBox.Password;
            PwdBox.Visibility = Visibility.Collapsed;
            TxtBox.Visibility = Visibility.Visible;
            TxtBox.Focus();
            TxtBox.CaretIndex = TxtBox.Text.Length;
            ToggleIcon.Text = "🙈";
        }
        else
        {
            PwdBox.Password = TxtBox.Text;
            TxtBox.Visibility = Visibility.Collapsed;
            PwdBox.Visibility = Visibility.Visible;
            PwdBox.Focus();
            ToggleIcon.Text = "👁";
        }
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UnlockDialogViewModel vm)
        {
            vm.Password = PwdBox.Password;
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void TxtBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is UnlockDialogViewModel vm)
        {
            vm.Password = TxtBox.Text;
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void PwdBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Unlock_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
        }
    }

    private void TxtBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Unlock_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
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
            string enteredPassword = _isPasswordVisible ? TxtBox.Text : PwdBox.Password;
            vm.Password = enteredPassword;

            if (string.IsNullOrEmpty(enteredPassword))
            {
                ErrorText.Text = "Please enter the folder password or Master PIN.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (ValidatePasswordAsync != null)
            {
                var error = await ValidatePasswordAsync(enteredPassword);
                if (!string.IsNullOrEmpty(error))
                {
                    ErrorText.Text = error;
                    ErrorText.Visibility = Visibility.Visible;

                    if (_isPasswordVisible)
                    {
                        TxtBox.SelectAll();
                        TxtBox.Focus();
                    }
                    else
                    {
                        PwdBox.SelectAll();
                        PwdBox.Focus();
                    }
                    return;
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
