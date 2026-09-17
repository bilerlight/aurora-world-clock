; Inno Setup script for Aurora World Clock.
; Build the portable payload first:  pwsh -File build.ps1 -SkipInstaller
; Then either run build.ps1 (auto-detects ISCC) or:
;   ISCC.exe /DAppDir="..\dist\app" /O"..\dist" setup.iss

#define AppName "Aurora World Clock"
#define AppVersion "1.2.0"
#define AppPublisher "Aurora"
#define AppExe "AuroraClock.exe"

#ifndef AppDir
  #define AppDir "..\dist\app"
#endif

[Setup]
AppId={{4B7E1C2A-9D63-4F51-8E77-2A1B6C93D501}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Aurora World Clock
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=AuroraClock-Setup-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
MinVersion=10.0

[Languages]
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"
Name: "startup";     Description: "开机自动启动";     GroupDescription: "附加任务:"

[Files]
Source: "{#AppDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExe}"
Name: "{group}\卸载 {#AppName}";       Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "AuroraWorldClock"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "立即启动 {#AppName}"; Flags: nowait postinstall skipifsilent


