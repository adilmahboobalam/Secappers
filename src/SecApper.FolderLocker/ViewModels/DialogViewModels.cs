using System;
using SecApper.Security.Ransomware;

namespace SecApper.FolderLocker.ViewModels;

public class LockFolderDialogViewModel : ViewModelBase
{
    private string _folderPath = string.Empty;
    private string _folderName = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _showPassword;
    private string? _errorMessage;

    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (SetProperty(ref _folderPath, value))
            {
                FolderName = System.IO.Path.GetFileName(value.TrimEnd('\\', '/'));
                if (string.IsNullOrWhiteSpace(FolderName))
                {
                    FolderName = value;
                }
            }
        }
    }

    public string FolderName
    {
        get => _folderName;
        set => SetProperty(ref _folderName, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public bool ShowPassword
    {
        get => _showPassword;
        set => SetProperty(ref _showPassword, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }
}

public class UnlockDialogViewModel : ViewModelBase
{
    private string _folderName = string.Empty;
    private string _folderPath = string.Empty;
    private string _password = string.Empty;
    private bool _showPassword;
    private string? _errorMessage;

    public string FolderName
    {
        get => _folderName;
        set => SetProperty(ref _folderName, value);
    }

    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool ShowPassword
    {
        get => _showPassword;
        set => SetProperty(ref _showPassword, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }
}

public class ChangePasswordDialogViewModel : ViewModelBase
{
    private string _folderName = string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmNewPassword = string.Empty;
    private bool _showPassword;
    private string? _errorMessage;

    public string FolderName
    {
        get => _folderName;
        set => SetProperty(ref _folderName, value);
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set => SetProperty(ref _currentPassword, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string ConfirmNewPassword
    {
        get => _confirmNewPassword;
        set => SetProperty(ref _confirmNewPassword, value);
    }

    public bool ShowPassword
    {
        get => _showPassword;
        set => SetProperty(ref _showPassword, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }
}

public class ThreatAlertViewModel : ViewModelBase
{
    public ThreatAlert Alert { get; }

    public ThreatAlertViewModel(ThreatAlert alert)
    {
        Alert = alert;
    }

    public string FolderPath => Alert.FolderPath;
    public string ProcessName => Alert.ProcessName ?? "Unknown Process";
    public string ProcessIdText => Alert.ProcessId.HasValue ? Alert.ProcessId.Value.ToString() : "N/A";
    public string ProcessPath => Alert.ProcessPath ?? "Unknown Path";
    public int ThreatScore => Alert.Assessment.Score;
    public string ThreatLevelText => Alert.Assessment.Level.ToString().ToUpper();
    public int AffectedFilesCount => Alert.Assessment.AffectedFilesCount;
    public string IndicatorsText => string.Join("\n• ", Alert.Assessment.Indicators);
    public string TimestampText => Alert.Timestamp.ToLocalTime().ToString("T");
}

public class SecurityEventItemViewModel : ViewModelBase
{
    public SecApper.Security.Models.SecurityEvent Model { get; }
    private string _folderNameDisplay;

    public SecurityEventItemViewModel(SecApper.Security.Models.SecurityEvent model, string folderNameDisplay = "General / Device")
    {
        Model = model;
        _folderNameDisplay = folderNameDisplay;
    }

    public string Id => Model.Id;
    public string EventType => Model.EventType;
    public string Description => Model.Description;
    public string ProcessName => string.IsNullOrWhiteSpace(Model.ProcessName) ? "explorer.exe" : Model.ProcessName;
    public string ProcessPath => Model.ProcessPath ?? "C:\\Windows\\explorer.exe";
    public string ProcessIdText => Model.ProcessId.HasValue ? Model.ProcessId.Value.ToString() : "N/A";
    public string ActionTaken => string.IsNullOrWhiteSpace(Model.ActionTaken) ? "Logged by SecApper engine" : Model.ActionTaken;
    public string FolderDisplay
    {
        get => _folderNameDisplay;
        set => SetProperty(ref _folderNameDisplay, value);
    }

    public string TimeText => Model.CreatedAt.ToLocalTime().ToString("hh:mm tt");
    public string FullTimeText => Model.CreatedAt.ToLocalTime().ToString("MMM dd, yyyy hh:mm:ss tt");
    public SecApper.Security.Models.EventSeverity Severity => Model.Severity;
    public string SeverityText => Model.Severity switch
    {
        SecApper.Security.Models.EventSeverity.Critical => "CRITICAL",
        SecApper.Security.Models.EventSeverity.High => "THREAT",
        SecApper.Security.Models.EventSeverity.Warning => "WARNING",
        _ => "INFO"
    };

    public System.Windows.Media.Brush SeverityBadgeBackground => Model.Severity switch
    {
        SecApper.Security.Models.EventSeverity.Critical or SecApper.Security.Models.EventSeverity.High =>
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226)), // #FEE2E2
        SecApper.Security.Models.EventSeverity.Warning =>
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199)), // #FEF3C7
        _ =>
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249))  // #F1F5F9
    };

    public System.Windows.Media.Brush SeverityBadgeForeground => Model.Severity switch
    {
        SecApper.Security.Models.EventSeverity.Critical or SecApper.Security.Models.EventSeverity.High =>
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(197, 32, 43)),   // #C5202B SecApper Red
        SecApper.Security.Models.EventSeverity.Warning =>
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9)),    // #B45309 Amber
        _ =>
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 45, 85))     // #122D55 Deep Navy
    };
}

