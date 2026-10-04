using System;
using System.Windows;
using System.Windows.Input;
using SecApper.Security.Security;

namespace SecApper.FolderLocker.Views;

public partial class SetupMasterPinDialog : Window
{
    private readonly IMasterPinService _masterPinService;

    public SetupMasterPinDialog(IMasterPinService masterPinService)
    {
        InitializeComponent();
        _masterPinService = masterPinService ?? throw new ArgumentNullException(nameof(masterPinService));
        Loaded += (s, e) => PinBox.Focus();
    }

    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ClearError();
    }

    private void ConfirmPinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ClearError();
    }

    private void PinBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmPinBox.Focus();
        }
    }

    private void ConfirmPinBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveButton_Click(sender, e);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string pin = PinBox.Password?.Trim() ?? string.Empty;
        string confirmPin = ConfirmPinBox.Password?.Trim() ?? string.Empty;

        var validation = _masterPinService.ValidatePin(pin);
        if (!validation.IsValid)
        {
            ShowError(validation.ErrorMessage ?? "Invalid Master PIN.");
            PinBox.Focus();
            return;
        }

        if (!string.Equals(pin, confirmPin, StringComparison.Ordinal))
        {
            ShowError("PIN confirmation does not match. Please re-enter.");
            ConfirmPinBox.Focus();
            return;
        }

        try
        {
            await _masterPinService.SetMasterPinAsync(pin);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to save Master PIN: {ex.Message}");
        }
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        ErrorText.Text = string.Empty;
    }
}
