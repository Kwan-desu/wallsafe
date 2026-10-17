; WallSafe 4.x (WebView2 edition) installer.
; Uses the same AppId as the 3.x WinUI installer so it upgrades the existing install in place
; (same folder, same Apps & Features entry). Build: ISCC.exe installer\WallSafeWeb.iss

#define MyAppName "WallSafe"
#define MyAppVersion "4.0.0"
#define MyAppPublisher "WallSafe Team"
#define MyAppURL "https://github.com/Kwan-desu/wallsafe"
#define MyAppExeName "WallSafe.exe"
#define RepoDir "C:\Users\ASUS\Documents\radom-ai-coded-shit\WallSafe"
#define SourceDir RepoDir + "\WallSafeWeb\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{D37F28D5-A1E4-4C2E-8E5A-6B1C94F9A712}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputBaseFilename=WallSafe-v{#MyAppVersion}-Setup
OutputDir={#RepoDir}\dist
SetupIconFile={#RepoDir}\WallSafeWeb\Assets\WallSafe.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=force
CloseApplicationsFilter=*.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Remove the WinUI 3 / Windows App SDK runtime left behind by 3.x so only the new build remains.
Type: files; Name: "{app}\WallSafeWinUI.*"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.pri"
Type: files; Name: "{app}\*.winmd"
Type: files; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\Assets"
Type: filesandordirs; Name: "{app}\Microsoft.UI.Xaml"
Type: filesandordirs; Name: "{app}\Views"
Type: filesandordirs; Name: "{app}\Controls"
Type: filesandordirs; Name: "{app}\wwwroot"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden; RunOnceId: "KillWallSafe"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\WallSafe\WebView2"
