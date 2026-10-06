import type { SecapperBridge } from '../types/bridge';
import type {
  FolderRecord,
  SecurityEvent,
  SystemSecurityStatus,
  RansomwareStatus,
  RecoveryReport,
  UpdateInfo,
  AppSettings,
  UserProfile,
  SetupInitialData,
  CompleteSetupPayload,
} from '../types';

type MessageHandler = (payload: any) => void;

class NativeBridgeService implements SecapperBridge {
  private messageIdCounter = 0;
  private pendingRequests = new Map<string, { resolve: (val: any) => void; reject: (err: any) => void }>();
  private eventListeners = new Map<string, Set<MessageHandler>>();
  private isNativeAvailable: boolean = false;

  constructor() {
    this.init();
  }

  private init() {
    if (typeof window !== 'undefined' && window.chrome && window.chrome.webview) {
      this.isNativeAvailable = true;
      window.chrome.webview.addEventListener('message', (event: any) => {
        const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (data.id && this.pendingRequests.has(data.id)) {
          const { resolve, reject } = this.pendingRequests.get(data.id)!;
          this.pendingRequests.delete(data.id);
          if (data.error) {
            reject(new Error(data.error));
          } else {
            resolve(data.result);
          }
        } else if (data.event) {
          const listeners = this.eventListeners.get(data.event);
          if (listeners) {
            listeners.forEach((callback) => callback(data.payload));
          }
        }
      });
    } else {
      this.isNativeAvailable = false;
      this.initFallbackState();
    }
  }

  private sendNative<T>(action: string, payload: any = {}): Promise<T> {
    if (this.isNativeAvailable && window.chrome?.webview) {
      return new Promise<T>((resolve, reject) => {
        const id = `req_${++this.messageIdCounter}_${Date.now()}`;
        this.pendingRequests.set(id, { resolve, reject });
        window.chrome!.webview!.postMessage({ id, action, payload });

        // 30s timeout safety
        setTimeout(() => {
          if (this.pendingRequests.has(id)) {
            this.pendingRequests.delete(id);
            reject(new Error(`Operation '${action}' timed out.`));
          }
        }, 30000);
      });
    }

    return this.handleFallback<T>(action, payload);
  }

  public on(event: string, callback: (data: any) => void): () => void {
    if (!this.eventListeners.has(event)) {
      this.eventListeners.set(event, new Set());
    }
    this.eventListeners.get(event)!.add(callback);

    return () => {
      this.eventListeners.get(event)?.delete(callback);
    };
  }

  // --- Folders API ---
  public folders = {
    list: async (): Promise<FolderRecord[]> => {
      return this.sendNative<FolderRecord[]>('folders.list');
    },

    lock: async (folderPath: string, password: string): Promise<{ success: boolean; folder?: FolderRecord; error?: string }> => {
      return this.sendNative('folders.lock', { folderPath, password });
    },

    unlock: async (folderId: string, password: string): Promise<{ success: boolean; folder?: FolderRecord; error?: string }> => {
      return this.sendNative('folders.unlock', { folderId, password });
    },

    removeProtection: async (folderId: string, password: string): Promise<{ success: boolean; error?: string }> => {
      return this.sendNative('folders.removeProtection', { folderId, password });
    },

    browseFolder: async (): Promise<string | null> => {
      return this.sendNative<string | null>('folders.browseFolder');
    },

    openFolder: async (folderId: string): Promise<boolean> => {
      return this.sendNative<boolean>('folders.openFolder', { folderId });
    },

    getDetails: async (folderId: string): Promise<FolderRecord | null> => {
      return this.sendNative<FolderRecord | null>('folders.getDetails', { folderId });
    },
  };

  // --- Security API ---
  public security = {
    getStatus: async (): Promise<SystemSecurityStatus> => {
      return this.sendNative<SystemSecurityStatus>('security.getStatus');
    },

    panicLock: async (): Promise<{ lockedCount: number; errors: string[] }> => {
      return this.sendNative('security.panicLock');
    },
  };

  // --- Ransomware API ---
  public ransomware = {
    getStatus: async (): Promise<RansomwareStatus> => {
      return this.sendNative<RansomwareStatus>('ransomware.getStatus');
    },

    toggle: async (enabled: boolean): Promise<boolean> => {
      return this.sendNative<boolean>('ransomware.toggle', { enabled });
    },

    dismissAlert: async (alertId: string): Promise<boolean> => {
      return this.sendNative<boolean>('ransomware.dismissAlert', { alertId });
    },
  };

  // --- Security Events API ---
  public events = {
    list: async (limit: number = 100): Promise<SecurityEvent[]> => {
      return this.sendNative<SecurityEvent[]>('events.list', { limit });
    },

    clear: async (): Promise<boolean> => {
      return this.sendNative<boolean>('events.clear');
    },

    exportCsv: async (): Promise<string> => {
      return this.sendNative<string>('events.exportCsv');
    },
  };

  // --- Recovery API ---
  public recovery = {
    check: async (): Promise<RecoveryReport> => {
      return this.sendNative<RecoveryReport>('recovery.check');
    },

    repair: async (issueId: string): Promise<{ success: boolean; message: string }> => {
      return this.sendNative('recovery.repair', { issueId });
    },

    restoreAccess: async (folderId: string): Promise<{ success: boolean; message: string }> => {
      return this.sendNative('recovery.restoreAccess', { folderId });
    },
  };

  // --- Updates API ---
  public updates = {
    check: async (): Promise<UpdateInfo> => {
      return this.sendNative<UpdateInfo>('updates.check');
    },

    download: async (): Promise<boolean> => {
      const res = await this.sendNative<any>('updates.download');
      if (res && res.success === false) {
        throw new Error(res.error || 'Failed to download update.');
      }
      return true;
    },

    install: async (): Promise<boolean> => {
      const res = await this.sendNative<any>('updates.install');
      if (res && res.success === false) {
        throw new Error(res.error || 'Failed to launch update installer.');
      }
      return true;
    },
  };

  // --- Settings API ---
  public settings = {
    get: async (): Promise<AppSettings> => {
      return this.sendNative<AppSettings>('settings.get');
    },

    update: async (settings: Partial<AppSettings>): Promise<AppSettings> => {
      return this.sendNative<AppSettings>('settings.update', settings);
    },
  };

  // --- Master PIN API ---
  public pin = {
    isConfigured: async (): Promise<boolean> => {
      return this.sendNative<boolean>('pin.isConfigured');
    },

    setup: async (pin: string): Promise<boolean> => {
      return this.sendNative<boolean>('pin.setup', { pin });
    },

    verify: async (pin: string): Promise<boolean> => {
      return this.sendNative<boolean>('pin.verify', { pin });
    },

    change: async (oldPin: string, newPin: string): Promise<boolean> => {
      return this.sendNative<boolean>('pin.change', { oldPin, newPin });
    },
  };

  // --- Window Management ---
  public window = {
    minimize: (): void => {
      if (this.isNativeAvailable) {
        window.chrome?.webview?.postMessage({ action: 'window.minimize' });
      }
    },
    maximize: (): void => {
      if (this.isNativeAvailable) {
        window.chrome?.webview?.postMessage({ action: 'window.maximize' });
      }
    },
    close: (): void => {
      if (this.isNativeAvailable) {
        window.chrome?.webview?.postMessage({ action: 'window.close' });
      }
    },
  };

  // --- Dynamic Setup API ---
  public setup = {
    isCompleted: async (): Promise<boolean> => {
      return this.sendNative<boolean>('setup.isCompleted');
    },

    getInitialData: async (): Promise<SetupInitialData> => {
      return this.sendNative<SetupInitialData>('setup.getInitialData');
    },

    complete: async (payload: CompleteSetupPayload): Promise<{ success: boolean; error?: string }> => {
      return this.sendNative<{ success: boolean; error?: string }>('setup.complete', payload);
    },

    reset: async (): Promise<boolean> => {
      return this.sendNative<boolean>('setup.reset');
    },
  };

  // --- User Profile API ---
  public profile = {
    get: async (): Promise<UserProfile> => {
      return this.sendNative<UserProfile>('profile.get');
    },

    update: async (payload: Partial<UserProfile>): Promise<{ success: boolean }> => {
      return this.sendNative<{ success: boolean }>('profile.update', payload);
    },
  };


  // --------------------------------------------------------------------------
  // BROWSER DEV FALLBACK (Active ONLY when running outside WebView2 during Vite dev)
  // --------------------------------------------------------------------------
  private fallbackFolders: FolderRecord[] = [];
  private fallbackEvents: SecurityEvent[] = [];
  private fallbackSettings: AppSettings = {
    autoCheckUpdates: true,
    updateFrequency: 'Daily',
    ransomwareProtectionEnabled: true,
    massModificationThreshold: 30,
    explorerIntegrationEnabled: true,
    masterPinConfigured: true,
    darkMode: false,
    startWithWindows: true,
    minimizeToTray: true,
  };
  private fallbackPin: string = '123456';
  private fallbackProfile: UserProfile = {
    userName: 'Security User',
    userRole: 'Security Administrator',
    avatar: 'shield-cyan',
    securityTier: 'Standard',
    installationId: 'SEC-8F92A1-4B29',
    setupDate: new Date().toISOString(),
    isCompleted: false,
  };

  private initFallbackState() {
    const storedFolders = localStorage.getItem('secapper_dev_folders');
    if (storedFolders) {
      try {
        this.fallbackFolders = JSON.parse(storedFolders);
      } catch {
        this.fallbackFolders = [];
      }
    } else {
      // Clean initial state (empty or 1 sample if user hasn't locked any)
      this.fallbackFolders = [
        {
          id: 'dev_fld_1',
          folderPath: 'C:\\Users\\User\\Documents\\Financial Records',
          folderName: 'Financial Records',
          status: 'Locked',
          createdAt: new Date(Date.now() - 86400000 * 2).toISOString(),
          updatedAt: new Date(Date.now() - 3600000 * 3).toISOString(),
          lastLockedAt: new Date(Date.now() - 3600000 * 3).toISOString(),
          protectionMode: 'Locked',
          iconStatus: 'LockedIconApplied',
        },
      ];
      this.saveFallbackFolders();
    }

    const storedEvents = localStorage.getItem('secapper_dev_events');
    if (storedEvents) {
      try {
        this.fallbackEvents = JSON.parse(storedEvents);
      } catch {
        this.fallbackEvents = [];
      }
    } else {
      this.fallbackEvents = [
        {
          id: 'evt_1',
          folderId: 'dev_fld_1',
          folderName: 'Financial Records',
          eventType: 'FolderLocked',
          processName: 'SecApper.FolderLocker.exe',
          description: 'Windows NTFS security permissions applied. Inheritance stripped and Deny ACE active.',
          createdAt: new Date(Date.now() - 3600000 * 3).toISOString(),
          severity: 'Info',
          actionTaken: 'Permissions Applied',
        },
        {
          id: 'evt_2',
          folderId: 'dev_fld_1',
          folderName: 'Financial Records',
          eventType: 'IntegrityVerified',
          processName: 'SecApper.Security.dll',
          description: 'Permission backup SDDL cryptographic hash verified successfully.',
          createdAt: new Date(Date.now() - 3600000 * 2.5).toISOString(),
          severity: 'Info',
          actionTaken: 'Verified',
        },
      ];
      this.saveFallbackEvents();
    }
  }

  private saveFallbackFolders() {
    localStorage.setItem('secapper_dev_folders', JSON.stringify(this.fallbackFolders));
  }

  private saveFallbackEvents() {
    localStorage.setItem('secapper_dev_events', JSON.stringify(this.fallbackEvents));
  }

  private async handleFallback<T>(action: string, payload: any): Promise<T> {
    // Artificial 120ms desktop latency for realistic UI state testing
    await new Promise((r) => setTimeout(r, 120));

    switch (action) {
      case 'folders.list':
        return [...this.fallbackFolders] as unknown as T;

      case 'folders.lock': {
        const { folderPath, password } = payload;
        if (!password || password.length < 4) {
          return { success: false, error: 'Password must be at least 4 characters.' } as unknown as T;
        }
        const folderName = folderPath.split('\\').filter(Boolean).pop() || folderPath;
        const newFolder: FolderRecord = {
          id: 'fld_' + Math.random().toString(36).substring(2, 9),
          folderPath,
          folderName,
          status: 'Locked',
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          lastLockedAt: new Date().toISOString(),
          protectionMode: 'Locked',
          iconStatus: 'LockedIconApplied',
        };
        this.fallbackFolders.unshift(newFolder);
        this.saveFallbackFolders();

        this.fallbackEvents.unshift({
          id: 'evt_' + Date.now(),
          folderId: newFolder.id,
          folderName: newFolder.folderName,
          eventType: 'FolderLocked',
          description: `Folder "${newFolder.folderName}" locked and Windows permissions applied.`,
          createdAt: new Date().toISOString(),
          severity: 'Info',
          actionTaken: 'Locked',
        });
        this.saveFallbackEvents();

        return { success: true, folder: newFolder } as unknown as T;
      }

      case 'folders.unlock': {
        const { folderId, password } = payload;
        const folder = this.fallbackFolders.find((f) => f.id === folderId);
        if (!folder) {
          return { success: false, error: 'Folder not found.' } as unknown as T;
        }
        if (password !== 'password' && password !== '1234' && password !== this.fallbackPin) {
          return { success: false, error: 'Incorrect password. Please try again.' } as unknown as T;
        }
        folder.status = 'Unlocked';
        folder.lastUnlockedAt = new Date().toISOString();
        folder.updatedAt = new Date().toISOString();
        this.saveFallbackFolders();

        this.fallbackEvents.unshift({
          id: 'evt_' + Date.now(),
          folderId: folder.id,
          folderName: folder.folderName,
          eventType: 'FolderUnlocked',
          description: `Folder "${folder.folderName}" unlocked and access restored.`,
          createdAt: new Date().toISOString(),
          severity: 'Info',
          actionTaken: 'Unlocked',
        });
        this.saveFallbackEvents();

        return { success: true, folder } as unknown as T;
      }

      case 'folders.removeProtection': {
        const { folderId, password } = payload;
        const index = this.fallbackFolders.findIndex((f) => f.id === folderId);
        if (index === -1) {
          return { success: false, error: 'Folder not found.' } as unknown as T;
        }
        if (password !== 'password' && password !== '1234' && password !== this.fallbackPin) {
          return { success: false, error: 'Incorrect password. Please try again.' } as unknown as T;
        }
        const removed = this.fallbackFolders.splice(index, 1)[0];
        this.saveFallbackFolders();

        this.fallbackEvents.unshift({
          id: 'evt_' + Date.now(),
          folderId: removed.id,
          folderName: removed.folderName,
          eventType: 'ProtectionRemoved',
          description: `SecApper protection removed for "${removed.folderName}". Filesystem permissions restored.`,
          createdAt: new Date().toISOString(),
          severity: 'Warning',
          actionTaken: 'Protection Removed',
        });
        this.saveFallbackEvents();

        return { success: true } as unknown as T;
      }

      case 'folders.browseFolder':
        return 'C:\\SecApper\\SecuredVault' as unknown as T;

      case 'folders.openFolder':
        return true as unknown as T;

      case 'folders.getDetails': {
        const folder = this.fallbackFolders.find((f) => f.id === payload.folderId) || null;
        return folder as unknown as T;
      }

      case 'security.getStatus': {
        const lockedCount = this.fallbackFolders.filter((f) => f.status === 'Locked').length;
        const totalCount = this.fallbackFolders.length;
        const status: SystemSecurityStatus = {
          status: 'PROTECTED',
          statusText: 'Your system is protected. SecApper is protecting your secured folders.',
          isProtected: true,
          protectedFoldersCount: totalCount,
          lockedFoldersCount: lockedCount,
          threatLevel: 'LOW',
          ransomwareActive: this.fallbackSettings.ransomwareProtectionEnabled,
          databaseHealthy: true,
          backupsAvailable: true,
          hasRecoveryIssues: false,
          masterPinConfigured: this.fallbackSettings.masterPinConfigured,
          explorerIntegrationActive: this.fallbackSettings.explorerIntegrationEnabled,
          version: '1.1.0',
        };
        return status as unknown as T;
      }

      case 'security.panicLock': {
        let count = 0;
        this.fallbackFolders.forEach((f) => {
          if (f.status === 'Unlocked') {
            f.status = 'Locked';
            f.lastLockedAt = new Date().toISOString();
            count++;
          }
        });
        this.saveFallbackFolders();
        return { lockedCount: count, errors: [] } as unknown as T;
      }

      case 'ransomware.getStatus': {
        const status: RansomwareStatus = {
          active: this.fallbackSettings.ransomwareProtectionEnabled,
          threatLevel: 'LOW',
          monitoredFoldersCount: this.fallbackFolders.length,
          suspiciousEventsToday: 0,
          blockedProcessesCount: 0,
          monitoringEngine: 'FileSystemWatcher + Heuristic Threat Scorer',
          recentAlerts: [],
        };
        return status as unknown as T;
      }

      case 'ransomware.toggle': {
        this.fallbackSettings.ransomwareProtectionEnabled = payload.enabled;
        return true as unknown as T;
      }

      case 'events.list':
        return [...this.fallbackEvents] as unknown as T;

      case 'events.clear':
        this.fallbackEvents = [];
        this.saveFallbackEvents();
        return true as unknown as T;

      case 'events.exportCsv': {
        const header = 'ID,Date,Severity,EventType,Folder,Process,Description\n';
        const rows = this.fallbackEvents
          .map((e) => `"${e.id}","${e.createdAt}","${e.severity}","${e.eventType}","${e.folderName || ''}","${e.processName || ''}","${e.description}"`)
          .join('\n');
        return (header + rows) as unknown as T;
      }

      case 'recovery.check': {
        const report: RecoveryReport = {
          databaseHealthy: true,
          permissionBackupsAvailable: true,
          foldersConsistent: true,
          recoveryServiceReady: true,
          issues: [],
          totalFoldersChecked: this.fallbackFolders.length,
          checkedAt: new Date().toISOString(),
        };
        return report as unknown as T;
      }

      case 'updates.check': {
        const update: UpdateInfo = {
          currentVersion: '1.1.0',
          availableVersion: '1.1.0',
          hasUpdate: false,
          downloadProgress: 0,
          status: 'Idle',
          statusMessage: "You're up to date.",
          lastChecked: new Date().toISOString(),
        };
        return update as unknown as T;
      }

      case 'settings.get':
        return { ...this.fallbackSettings } as unknown as T;

      case 'settings.update':
        this.fallbackSettings = { ...this.fallbackSettings, ...payload };
        return { ...this.fallbackSettings } as unknown as T;

      case 'pin.isConfigured':
        return this.fallbackSettings.masterPinConfigured as unknown as T;

      case 'pin.setup':
        this.fallbackPin = payload.pin;
        this.fallbackSettings.masterPinConfigured = true;
        return true as unknown as T;

      case 'pin.verify':
        return (payload.pin === this.fallbackPin) as unknown as T;

      case 'pin.change':
        if (payload.oldPin === this.fallbackPin) {
          this.fallbackPin = payload.newPin;
          return true as unknown as T;
        }
        return false as unknown as T;

      case 'setup.isCompleted': {
        if (typeof localStorage !== 'undefined') {
          const completed = localStorage.getItem('secapper_setup_completed');
          if (completed !== null) {
            this.fallbackProfile.isCompleted = completed === 'true';
          }
        }
        return this.fallbackProfile.isCompleted as unknown as T;
      }

      case 'setup.getInitialData': {
        const initData: SetupInitialData = {
          suggestedUsername: 'Security User',
          machineName: 'DESKTOP-SECURE',
          osVersion: 'Windows 11 Pro 64-bit',
          installationId: this.fallbackProfile.installationId,
          hasMasterPin: this.fallbackSettings.masterPinConfigured,
        };
        return initData as unknown as T;
      }

      case 'setup.complete': {
        this.fallbackProfile.userName = payload.userName || this.fallbackProfile.userName;
        this.fallbackProfile.userRole = payload.userRole || this.fallbackProfile.userRole;
        this.fallbackProfile.avatar = payload.avatar || this.fallbackProfile.avatar;
        this.fallbackProfile.securityTier = payload.securityTier || 'Standard';
        this.fallbackProfile.isCompleted = true;
        this.fallbackProfile.setupDate = new Date().toISOString();
        if (payload.masterPin) {
          this.fallbackPin = payload.masterPin;
          this.fallbackSettings.masterPinConfigured = true;
        }
        if (typeof localStorage !== 'undefined') {
          localStorage.setItem('secapper_setup_completed', 'true');
          localStorage.setItem('secapper_profile', JSON.stringify(this.fallbackProfile));
        }
        return { success: true } as unknown as T;
      }

      case 'setup.reset': {
        this.fallbackProfile.isCompleted = false;
        if (typeof localStorage !== 'undefined') {
          localStorage.removeItem('secapper_setup_completed');
        }
        return true as unknown as T;
      }

      case 'profile.get': {
        if (typeof localStorage !== 'undefined') {
          const stored = localStorage.getItem('secapper_profile');
          if (stored) {
            try {
              this.fallbackProfile = { ...this.fallbackProfile, ...JSON.parse(stored) };
            } catch {}
          }
          if (localStorage.getItem('secapper_setup_completed') === 'true') {
            this.fallbackProfile.isCompleted = true;
          }
        }
        return { ...this.fallbackProfile } as unknown as T;
      }

      case 'profile.update': {
        this.fallbackProfile = { ...this.fallbackProfile, ...payload };
        if (typeof localStorage !== 'undefined') {
          localStorage.setItem('secapper_profile', JSON.stringify(this.fallbackProfile));
        }
        return { success: true } as unknown as T;
      }

      default:
        throw new Error(`Unknown action: ${action}`);
    }
  }
}

export const nativeBridge = new NativeBridgeService();
if (typeof window !== 'undefined') {
  window.secapper = nativeBridge;
}
export default nativeBridge;
