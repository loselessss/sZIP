#define MyAppName "sZIP"
#define MyAppVersion "1.8.1"
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
UsePreviousLanguage=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Default.isl,installer.ko.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
korean.DesktopShortcut=바탕 화면 바로가기 만들기
korean.AdditionalShortcuts=추가 바로가기:
korean.StartWithWindows=Windows 로그인 시 트레이에서 실행
korean.StartupOptions=시작 프로그램:
korean.ExplorerIntegration=탐색기 sZIP 메뉴 및 압축 파일 연결 등록
korean.ExplorerOptions=Windows 탐색기:
english.DesktopShortcut=Create a desktop shortcut
english.AdditionalShortcuts=Additional shortcuts:
english.StartWithWindows=Start in the tray when signing in to Windows
english.StartupOptions=Startup:
english.ExplorerIntegration=Register the sZIP Explorer menu and archive file associations
english.ExplorerOptions=Windows Explorer:

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartWithWindows}"; GroupDescription: "{cm:StartupOptions}"; Flags: unchecked
Name: "shellintegration"; Description: "{cm:ExplorerIntegration}"; GroupDescription: "{cm:ExplorerOptions}"; Flags: checkedonce

[Files]
Source: "artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\sZIP"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\sZIP"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "sZIP"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register-shell"; Flags: runhidden waituntilterminated; Tasks: shellintegration
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister-shell"; Flags: runhidden waituntilterminated; Tasks: not shellintegration
Filename: "{cmd}"; Parameters: "/C ping 127.0.0.1 -n 4 > nul & del /f /q ""{param:DELETEINSTALLER|}"""; Flags: runhidden nowait; Check: ShouldDeleteInstaller
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,sZIP}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister-shell"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "UnregisterSzipShell"

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
