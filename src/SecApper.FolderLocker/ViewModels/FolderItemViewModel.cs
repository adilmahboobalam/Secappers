using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using SecApper.Security.Models;

namespace SecApper.FolderLocker.ViewModels;

public class FolderItemViewModel : ViewModelBase
{
    private readonly FolderRecord _model;

    public FolderItemViewModel(FolderRecord model)
    {
        _model = model;
    }

    public FolderRecord Model => _model;

    public string Id => _model.Id;
    public string FolderPath => _model.FolderPath;
    public string FolderName => _model.FolderName;

    public FolderStatus Status
    {
        get => _model.Status;
        set
        {
            if (_model.Status != value)
            {
                _model.Status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBadgeColor));
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(IsUnlocked));
                OnPropertyChanged(nameof(IsInconsistent));
            }
        }
    }

    public string StatusText => _model.Status switch
    {
        FolderStatus.Locked => "LOCKED",
        FolderStatus.Unlocked => "UNLOCKED",
        FolderStatus.Inconsistent => "INCONSISTENT",
        FolderStatus.Missing => "MISSING ON DISK",
        _ => "UNKNOWN"
    };

    public System.Windows.Media.Brush StatusBadgeColor => _model.Status switch
    {
        FolderStatus.Locked => new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)), // Red
        FolderStatus.Unlocked => new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)), // Green
        FolderStatus.Inconsistent => new SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8)), // Yellow
        _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)) // Grey
    };

    public bool IsLocked => _model.Status == FolderStatus.Locked;
    public bool IsUnlocked => _model.Status == FolderStatus.Unlocked;
    public bool IsInconsistent => _model.Status == FolderStatus.Inconsistent;

    public string ProtectionModeText => _model.ProtectionMode switch
    {
        ProtectionMode.LockedAndProtected => "🔒 LOCKED + 🛡 PROTECTED",
        ProtectionMode.Locked => "🔒 LOCKED (ACL)",
        ProtectionMode.Protected => "🛡 RANSOMWARE PROTECTED",
        _ => "NONE"
    };

    public string CreatedAtText => _model.CreatedAt.ToLocalTime().ToString("g");
    public string LastLockedText => _model.LastLockedAt.HasValue ? _model.LastLockedAt.Value.ToLocalTime().ToString("g") : "Never";
    public string LastUnlockedText => _model.LastUnlockedAt.HasValue ? _model.LastUnlockedAt.Value.ToLocalTime().ToString("g") : "Never";

    public string ProtectionShortText => "Protected";

    public string LastActivityText
    {
        get
        {
            if (_model.LastLockedAt.HasValue && _model.LastUnlockedAt.HasValue)
            {
                return _model.LastLockedAt.Value > _model.LastUnlockedAt.Value
                    ? $"Locked {_model.LastLockedAt.Value.ToLocalTime():MMM dd, HH:mm}"
                    : $"Unlocked {_model.LastUnlockedAt.Value.ToLocalTime():MMM dd, HH:mm}";
            }
            if (_model.LastLockedAt.HasValue)
                return $"Locked {_model.LastLockedAt.Value.ToLocalTime():MMM dd, HH:mm}";
            if (_model.LastUnlockedAt.HasValue)
                return $"Unlocked {_model.LastUnlockedAt.Value.ToLocalTime():MMM dd, HH:mm}";
            return $"Added {_model.CreatedAt.ToLocalTime():MMM dd, HH:mm}";
        }
    }

    public System.Windows.Media.Brush StatusBadgeBackground => _model.Status switch
    {
        FolderStatus.Locked => new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226)), // #FEE2E2
        FolderStatus.Unlocked => new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231)), // #DCFCE7
        FolderStatus.Inconsistent => new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199)), // #FEF3C7
        _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249))
    };

    public System.Windows.Media.Brush StatusBadgeForeground => _model.Status switch
    {
        FolderStatus.Locked => new SolidColorBrush(System.Windows.Media.Color.FromRgb(197, 32, 43)), // SecApper Red #C5202B
        FolderStatus.Unlocked => new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52)), // Green #166534
        FolderStatus.Inconsistent => new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9)), // Amber #B45309
        _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139))
    };

    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBadgeColor));
        OnPropertyChanged(nameof(StatusBadgeBackground));
        OnPropertyChanged(nameof(StatusBadgeForeground));
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(IsInconsistent));
        OnPropertyChanged(nameof(LastLockedText));
        OnPropertyChanged(nameof(LastUnlockedText));
        OnPropertyChanged(nameof(LastActivityText));
    }

    public void OpenInExplorer()
    {
        try
        {
            if (Directory.Exists(_model.FolderPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{_model.FolderPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore explorer launch issues
        }
    }
}
