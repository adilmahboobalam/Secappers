; SecApper Folder Locker — Inno Setup Script
; Offline Windows Folder Security & Ransomware Protection

#define MyAppName "SecApper Folder Locker"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "SecApper Cybersecurity"
#define MyAppExeName "SecApper.FolderLocker.exe"
#define MyAppAssocName "SecApper Protected Folder"

[Setup]
AppId={{5E9110B0-4D54-4C2E-8E5F-B7E05C557F83}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SecApper Folder Locker
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\installer\output
OutputBaseFilename=SecApperFolderLockerSetup_v1.1.0
SetupIconFile=..\src\SecApper.FolderLocker\Resources\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
DisableDirPage=no
DisableProgramGroupPage=no
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "explorermenu"; Description: "Add 'Lock with SecApper' and 'Unlock' to Windows Explorer context menu"; GroupDescription: "Windows Explorer Integration:"
Name: "startup"; Description: "Start SecApper automatically with Windows"; GroupDescription: "Windows Integration:"; Flags: checkedonce

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace
Source: "..\src\SecApper.FolderLocker\Resources\app.ico"; DestDir: "{commonappdata}\SecApper\FolderLocker\icons"; DestName: "locked.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; Windows Explorer context menu integration - Folder
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperFolderLocker"; ValueType: string; ValueData: "SecApper Folder Locker"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperFolderLocker"; ValueName: "Icon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperFolderLocker\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: explorermenu

; Windows Explorer context menu integration - Direct Unlock
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperUnlock"; ValueType: string; ValueData: "🔓 Unlock with SecApper"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperUnlock"; ValueName: "Icon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperUnlock\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --unlock ""%1"""; Tasks: explorermenu

; Windows Explorer context menu integration - Folder Background
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SecApperFolderLocker"; ValueType: string; ValueData: "SecApper Folder Locker"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SecApperFolderLocker"; ValueName: "Icon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SecApperFolderLocker\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%V"""; Tasks: explorermenu

; Windows Explorer context menu integration - Drives
Root: HKCU; Subkey: "Software\Classes\Drive\shell\SecApperFolderLocker"; ValueType: string; ValueData: "SecApper Folder Locker"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Drive\shell\SecApperFolderLocker"; ValueName: "Icon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Drive\shell\SecApperFolderLocker\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: explorermenu

; Windows App Paths registration (allows running executable directly)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueName: "Path"; ValueType: string; ValueData: "{app}"; Flags: uninsdeletekey

; Windows Startup integration (Start with Windows in background tray)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "SecApperFolderLocker"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: startup

; Application metadata key
Root: HKCU; Subkey: "Software\SecApper"; Flags: uninsdeletekey

[UninstallDelete]
; Clean up runtime files and data directories completely upon uninstall
Type: filesandordirs; Name: "{commonappdata}\SecApper"
Type: filesandordirs; Name: "{localappdata}\SecApper"
Type: filesandordirs; Name: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall

[Code]
// Pre-installation cleanup: safely terminate existing instances to avoid file locks
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM SecApper.FolderLocker.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM SecApper.Updater.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
  Result := '';
end;

// Safety check on uninstall to ensure user does not inadvertently leave protected folders without unlocking
function InitializeUninstall(): Boolean;
var
  DbPath: String;
begin
  Result := True;
  DbPath := ExpandConstant('{commonappdata}\SecApper\FolderLocker\locker.db');
  if FileExists(DbPath) then
  begin
    if MsgBox('SecApper Folder Locker is about to be uninstalled.' + #13#10 + #13#10 +
              'IMPORTANT: Please ensure you have unlocked any protected folders before removing the application.' + #13#10 + #13#10 +
              'Do you want to continue with the uninstallation?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;

// Complete cleanup of all application data, caches, databases, and registry upon uninstallation
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Terminate any running instances before file removal
    Exec('taskkill.exe', '/F /IM SecApper.FolderLocker.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/F /IM SecApper.Updater.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    // Thorough removal of all application databases, icons, logs, and caches
    DelTree(ExpandConstant('{commonappdata}\SecApper'), True, True, True);
    DelTree(ExpandConstant('{localappdata}\SecApper'), True, True, True);
    DelTree(ExpandConstant('{app}'), True, True, True);

    // Clean up all Windows Explorer shell integration and registry keys
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\SecApperFolderLocker');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\shell\SecApperUnlock');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Directory\Background\shell\SecApperFolderLocker');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Drive\shell\SecApperFolderLocker');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\SecApper');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SecApperFolderLocker');
  end;
end;
