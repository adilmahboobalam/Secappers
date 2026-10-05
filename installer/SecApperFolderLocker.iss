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
ArchitecturesInstallIn64BitMode=x64
CloseApplications=yes
CloseApplicationsFilter=SecApper.FolderLocker.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "explorermenu"; Description: "Add 'Lock with SecApper' to Windows Explorer context menu"; GroupDescription: "Windows Explorer Integration:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\SecApper.FolderLocker\Resources\app.ico"; DestDir: "{commonappdata}\SecApper\FolderLocker\icons"; DestName: "locked.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; Windows Explorer context menu integration
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperFolderLocker"; ValueType: string; ValueData: "SecApper Folder Locker"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperFolderLocker"; ValueName: "Icon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SecApperFolderLocker\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: explorermenu

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
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
