#define MyAppName "WallSafe"
#define MyAppVersion "3.1.1"
#define MyAppPublisher "WallSafe Team"
#define MyAppURL "https://github.com/Kwan-desu/wallsafe"
#define MyAppExeName "WallSafeWinUI.exe"
#define SourceDir "C:\Users\ASUS\Documents\radom-ai-coded-shit\WallSafe\WallSafeWinUI\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{D37F28D5-A1E4-4C2E-8E5A-6B1C94F9A712}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputBaseFilename=WallSafe-v3.1.0-Setup
OutputDir=C:\Users\ASUS\Documents\radom-ai-coded-shit\WallSafe
SetupIconFile=C:\Users\ASUS\Documents\radom-ai-coded-shit\WallSafe\WallSafeWinUI\Assets\WallSafe.ico
UninstallDisplayIcon={app}\Assets\WallSafe.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter=WallSafeWinUI.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "C:\Users\ASUS\Documents\radom-ai-coded-shit\WallSafe\WallSafeWinUI\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\WallSafeWinUI.pri"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\ASUS\Documents\radom-ai-coded-shit\WallSafe\WallSafeWinUI\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\WallSafeWinUI.pri"; DestName: "resources.pri"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\WallSafe.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\WallSafe.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
