import type {
  FolderRecord,
  SecurityEvent,
  SystemSecurityStatus,
  RansomwareStatus,
  RecoveryReport,
  UpdateInfo,
  AppSettings,
} from './index';

export interface SecapperBridge {
  folders: {
    list: () => Promise<FolderRecord[]>;
    lock: (folderPath: string, password: string) => Promise<{ success: boolean; folder?: FolderRecord; error?: string }>;
    unlock: (folderId: string, password: string) => Promise<{ success: boolean; folder?: FolderRecord; error?: string }>;
    removeProtection: (folderId: string, password: string) => Promise<{ success: boolean; error?: string }>;
    browseFolder: () => Promise<string | null>;
    openFolder: (folderId: string) => Promise<boolean>;
    getDetails: (folderId: string) => Promise<FolderRecord | null>;
  };
  security: {
    getStatus: () => Promise<SystemSecurityStatus>;
    panicLock: () => Promise<{ lockedCount: number; errors: string[] }>;
  };
  ransomware: {
    getStatus: () => Promise<RansomwareStatus>;
    toggle: (enabled: boolean) => Promise<boolean>;
    dismissAlert: (alertId: string) => Promise<boolean>;
  };
  events: {
    list: (limit?: number) => Promise<SecurityEvent[]>;
    clear: () => Promise<boolean>;
    exportCsv: () => Promise<string>;
  };
  recovery: {
    check: () => Promise<RecoveryReport>;
    repair: (issueId: string) => Promise<{ success: boolean; message: string }>;
    restoreAccess: (folderId: string) => Promise<{ success: boolean; message: string }>;
  };
  updates: {
    check: () => Promise<UpdateInfo>;
    download: () => Promise<boolean>;
    install: () => Promise<boolean>;
  };
  settings: {
    get: () => Promise<AppSettings>;
    update: (settings: Partial<AppSettings>) => Promise<AppSettings>;
  };
  pin: {
    isConfigured: () => Promise<boolean>;
    setup: (pin: string) => Promise<boolean>;
    verify: (pin: string) => Promise<boolean>;
    change: (oldPin: string, newPin: string) => Promise<boolean>;
  };
  window: {
    minimize: () => void;
    maximize: () => void;
    close: () => void;
  };
  on: (event: string, callback: (data: any) => void) => () => void;
}
