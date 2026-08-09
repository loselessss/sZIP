#define MyAppName "sZIP"
#define MyAppVersion "1.3.0"
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
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로가기 만들기"; GroupDescription: "추가 바로가기:"; Flags: unchecked
Name: "startup"; Description: "Windows 로그인 시 트레이에서 시작"; GroupDescription: "자동 시작:"; Flags: unchecked
Name: "shellintegration"; Description: "탐색기 압축/해제 메뉴와 압축 파일 연결 등록"; GroupDescription: "Windows 탐색기:"; Flags: checkedonce

[Files]
Source: "artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\sZIP"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\sZIP"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "sZIP"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Tasks: startup; Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\Classes\sZIP.Archive"; ValueType: string; ValueData: "sZIP 압축 파일"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\sZIP.Archive\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\sZIP.Archive\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --open ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\.zip\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.7z\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.rar\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.tar\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.gz\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.tgz\OpenWithProgids"; ValueType: none; ValueName: "sZIP.Archive"; Tasks: shellintegration; Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\Classes\*\shell\sZIP.compress"; ValueType: string; ValueData: "sZIP으로 압축"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\sZIP.compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\*\shell\sZIP.compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\*\shell\sZIP.compress\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --compress ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\Directory\shell\sZIP.compress"; ValueType: string; ValueData: "sZIP으로 압축"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\sZIP.compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\Directory\shell\sZIP.compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\Directory\shell\sZIP.compress\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --compress ""%1"""; Tasks: shellintegration

Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\sZIP.extract"; ValueType: string; ValueData: "sZIP으로 압축 풀기"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\sZIP.extract"; ValueType: string; ValueData: "sZIP으로 압축 풀기"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\sZIP.extract"; ValueType: string; ValueData: "sZIP으로 압축 풀기"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\sZIP.extract"; ValueType: string; ValueData: "sZIP으로 압축 풀기"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\sZIP.extract"; ValueType: string; ValueData: "sZIP으로 압축 풀기"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\sZIP.extract"; ValueType: string; ValueData: "sZIP으로 압축 풀기"; Tasks: shellintegration; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\sZIP.extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\sZIP.extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\sZIP.extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\sZIP.extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\sZIP.extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract ""%1"""; Tasks: shellintegration
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tgz\shell\sZIP.extract\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --extract ""%1"""; Tasks: shellintegration

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "sZIP 실행"; Flags: nowait postinstall skipifsilent
