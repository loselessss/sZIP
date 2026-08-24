#define MyAppName "sZIP"
#define MyAppVersion "1.6.0"
#define MyAppPublisher "sZIP contributors"
#define MyAppExeName "sZIP.App.exe"

[Setup]
AppId={{21619582-4344-4DA7-A6D8-844C78BB3FE9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\sZIP
DefaultGroupName=sZIP
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
OutputDir=artifacts
OutputBaseFilename=sZIP_Setup_{#MyAppVersion}
SetupIconFile=src\sZIP.App\Assets\szip.ico
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start in the tray when signing in to Windows"; GroupDescription: "Startup:"; Flags: unchecked
Name: "shellintegration"; Description: "Register Explorer archive menus and file associations"; GroupDescription: "Windows Explorer:"; Flags: checkedonce

[Files]
Source: "artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\sZIP"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\sZIP"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "sZIP"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Tasks: startup; Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\Classes\sZIP.Archive"; ValueType: string; ValueData: "sZIP archive file"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\sZIP.Archive\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\sZIP.Archive\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --open ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\.zip\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.7z\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.rar\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.tar\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.gz\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.tgz\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\Classes\*\shell\sZIP.compress"; ValueType: string; ValueData: "Compress with sZIP"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\sZIP.compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\*\shell\sZIP.compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\*\shell\sZIP.compress\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --compress ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\Directory\shell\sZIP.compress"; ValueType: string; ValueData: "Compress with sZIP"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\sZIP.compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\Directory\shell\sZIP.compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\Directory\shell\sZIP.compress\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --compress ""%1"""; Tasks: shellintegration

Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\sZIP.extract-direct"; ValueType: string; ValueData: "sZIP Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\sZIP.extract-direct"; ValueType: string; ValueData: "sZIP Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\sZIP.extract-direct"; ValueType: string; ValueData: "sZIP Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\sZIP.extract-direct"; ValueType: string; ValueData: "sZIP Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\sZIP.extract-direct"; ValueType: string; ValueData: "sZIP Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\sZIP.extract-direct"; ValueType: string; ValueData: "sZIP Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\sZIP.extract-direct\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-direct ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\sZIP.extract-direct\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-direct ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\sZIP.extract-direct\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-direct ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\sZIP.extract-direct\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-direct ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\sZIP.extract-direct\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-direct ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\sZIP.extract-direct\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-direct ""%1"""; Tasks: shellintegration

Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\sZIP.extract-smart"; ValueType: string; ValueData: "sZIP Smart Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\sZIP.extract-smart"; ValueType: string; ValueData: "sZIP Smart Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\sZIP.extract-smart"; ValueType: string; ValueData: "sZIP Smart Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\sZIP.extract-smart"; ValueType: string; ValueData: "sZIP Smart Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\sZIP.extract-smart"; ValueType: string; ValueData: "sZIP Smart Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\sZIP.extract-smart"; ValueType: string; ValueData: "sZIP Smart Extract"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\sZIP.extract-smart\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-smart ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\sZIP.extract-smart\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-smart ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\sZIP.extract-smart\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-smart ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\sZIP.extract-smart\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-smart ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\sZIP.extract-smart\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-smart ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\sZIP.extract-smart\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract-smart ""%1"""; Tasks: shellintegration

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-modern-shell"; Flags: runhidden waituntilterminated; Tasks: shellintegration
Filename: "{cmd}"; Parameters: "/C ping 127.0.0.1 -n 4 > nul & del /f /q ""{param:DELETEINSTALLER|}"""; Flags: runhidden nowait; Check: ShouldDeleteInstaller
Filename: "{app}\{#MyAppExeName}"; Description: "Launch sZIP"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister-modern-shell"; Flags: runhidden waituntilterminated skipifdoesntexist

[Code]
function ShouldDeleteInstaller: Boolean;
var
  InstallerPath: String;
  InstallerName: String;
begin
  InstallerPath := ExpandConstant('{param:DELETEINSTALLER|}');
  InstallerName := ExtractFileName(InstallerPath);
  Result :=
    (InstallerPath <> '') and
    (CompareText(ExtractFileExt(InstallerPath), '.exe') = 0) and
    (Pos('sZIP_Setup_', InstallerName) = 1);
end;
