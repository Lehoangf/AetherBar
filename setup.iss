; AetherBar Inno Setup Script
; Requires Inno Setup 6+ (https://jrsoftware.org/isdl.php)
;
; Compile:
;   iscc setup.iss
; Or from publish.ps1 output:
;   1. Run .\publish.ps1 -NoZip
;   2. iscc setup.iss

#define MyAppName "AetherBar"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "AetherBar"
#define MyAppURL "https://github.com/yourusername/AetherBar"
#define MyAppExeName "AetherBar.UI.exe"
#define MyAppAssocName MyAppName + " File"
#define MyAppAssocExt ".myp"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
AppId={{B8A3C8E2-1C4A-4F2D-9E7F-8A5D3C6B2E1F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=AetherBar-Setup-{#MyAppVersion}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=AetherBar.UI\Assets\AetherBar.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startup"; Description: "&Start with Windows automatically"; GroupDescription: "Startup options:"

[Files]
Source: "publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch AetherBar"; Flags: postinstall nowait skipifsilent shellexec
Filename: "{reg:HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\dotnet.exe,}"; Parameters: "run --project ""{src}\AetherBar.UI"" -c Release"; Description: "Build & Run from Source (requires .NET SDK)"; Flags: postinstall nowait skipifsilent unchecked shellexec

[Registry]
; Add startup entry if task selected
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AetherBar"; ValueData: "{app}\{#MyAppExeName}"; Tasks: startup; Flags: uninsdeletevalue

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall"; RunOnceId: "AetherBarUninstall"

[Code]
function InitializeSetup: Boolean;
begin
  Result := True;
end;
