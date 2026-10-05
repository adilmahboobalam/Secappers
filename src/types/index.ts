export type FolderStatus =
  | 'Unlocked'
  | 'Locking'
  | 'Locked'
  | 'Unlocking'
  | 'RecoveryRequired'
  | 'Error'
  | 'Inconsistent'
  | 'Missing';

export type IconStatus = 'Default' | 'LockedIconApplied' | 'Failed';

export type ProtectionMode = 'None' | 'Locked' | 'Protected' | 'LockedAndProtected';

export type EventSeverity = 'Info' | 'Warning' | 'High' | 'Critical';

export type ThreatLevel = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';

export interface FolderRecord {
  id: string;
  folderPath: string;
  folderName: string;
  status: FolderStatus;
  createdAt: string;
  updatedAt: string;
  lastLockedAt?: string;
  lastUnlockedAt?: string;
  protectionMode: ProtectionMode;
  iconStatus: IconStatus;
  originalAclBackupId?: string;
}

export interface SecurityEvent {
  id: string;
  folderId?: string;
  folderName?: string;
  eventType: string;
  processName?: string;
  processPath?: string;
  processId?: number;
  description: string;
  createdAt: string;
  severity: EventSeverity;
  actionTaken: string;
}

export interface ProtectionPolicy {
  id: string;
  folderId: string;
  protectionEnabled: boolean;
  blockUnknownProcesses: boolean;
  massModificationThreshold: number;
  deleteProtection: boolean;
  renameProtection: boolean;
  extensionChangeProtection: boolean;
}

export interface RecoveryIssue {
  id: string;
  folderId?: string;
  folderPath: string;
  folderName: string;
  issueType: string;
  description: string;
  recommendedAction: string;
  canAutoRepair: boolean;
}

export interface RecoveryReport {
  databaseHealthy: boolean;
  permissionBackupsAvailable: boolean;
  foldersConsistent: boolean;
  recoveryServiceReady: boolean;
  issues: RecoveryIssue[];
  totalFoldersChecked: number;
  checkedAt: string;
}

export interface RansomwareStatus {
  active: boolean;
  threatLevel: ThreatLevel;
  monitoredFoldersCount: number;
  suspiciousEventsToday: number;
  blockedProcessesCount: number;
  monitoringEngine: 'FileSystemWatcher + Heuristic Threat Scorer';
  recentAlerts: SecurityEvent[];
}

export interface SystemSecurityStatus {
  status: 'PROTECTED' | 'ATTENTION' | 'CRITICAL';
  statusText: string;
  isProtected: boolean;
  protectedFoldersCount: number;
  lockedFoldersCount: number;
  threatLevel: ThreatLevel;
  ransomwareActive: boolean;
  databaseHealthy: boolean;
  backupsAvailable: boolean;
  hasRecoveryIssues: boolean;
  masterPinConfigured: boolean;
  explorerIntegrationActive: boolean;
  version: string;
}

export interface UpdateInfo {
  currentVersion: string;
  availableVersion?: string;
  hasUpdate: boolean;
  releaseNotes?: string;
  mandatory?: boolean;
  status: 'Idle' | 'Checking' | 'Available' | 'Downloading' | 'Verifying' | 'Installing' | 'Ready' | 'Error';
  downloadProgress: number;
  statusMessage?: string;
  lastChecked?: string;
}

export interface AppSettings {
  autoCheckUpdates: boolean;
  updateFrequency: 'Daily' | 'Weekly' | 'Manual';
  ransomwareProtectionEnabled: boolean;
  massModificationThreshold: number;
  explorerIntegrationEnabled: boolean;
  masterPinConfigured: boolean;
  darkMode: boolean;
  startWithWindows: boolean;
  minimizeToTray: boolean;
}
