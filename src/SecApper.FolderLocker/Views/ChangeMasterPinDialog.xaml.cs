using System;
using System.Windows;
using SecApper.Security.Security;

namespace SecApper.FolderLocker.Views;

public partial class ChangeMasterPinDialog : Window
{
    private readonly IMasterPinService _masterPinService;

    public ChangeMasterPinDialog(IMasterPinService masterPinService)
    {
        InitializeComponent();
        _masterPinService = masterPinService ?? throw new ArgumentNullException(nameof(masterPinService));
        Loaded += (s, e) => CurrentBox.Focus();
    }

    private void CurrentBox_PasswordChanged(object sender, RoutedEventArgs e) => ClearError();
    private void NewBox_PasswordChanged(object sender, RoutedEventArgs e) => ClearError();
    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e) => ClearError();

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string currentPin = CurrentBox.Password?.Trim() ?? string.Empty;
        string newPin = NewBox.Password?.Trim() ?? string.Empty;
        string confirmPin = ConfirmBox.Password?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(currentPin))
        {
            ShowError("Please enter your current Master PIN.");
            CurrentBox.Focus();
            return;
        }

        var validation = _masterPinService.ValidatePin(newPin);
        if (!validation.IsValid)
        {
            ShowError(validation.ErrorMessage ?? "Invalid new Master PIN.");
            NewBox.Focus();
            return;
        }

        if (!string.Equals(newPin, confirmPin, StringComparison.Ordinal))
        {
            ShowError("New PIN confirmation does not match.");
            ConfirmBox.Focus();
            return;
        }

        try
        {
            var result = await _masterPinService.ChangeMasterPinAsync(currentPin, newPin);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage ?? "Current Master PIN is incorrect.");
                CurrentBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to update Master PIN: {ex.Message}");
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
