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

[Code]
// Upgrading from 3.x (WinUI 3) leaves hundreds of runtime files and locale folders behind.
// Before copying, empty the install folder (keeping the uninstaller) — but only when it is
// clearly a WallSafe folder, so a custom directory with unrelated files is never touched.
procedure CleanAppDir(Dir: String);
var
  FR: TFindRec;
  Name: String;
begin
  if not (FileExists(Dir + '\WallSafe.exe') or FileExists(Dir + '\WallSafeWinUI.exe')) then Exit;
  if FindFirst(Dir + '\*', FR) then
  try
    repeat
      Name := FR.Name;
      if (Name = '.') or (Name = '..') or (Pos('unins', LowerCase(Name)) = 1) then Continue;
      if FR.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then
        DelTree(Dir + '\' + Name, True, True, True)
      else
        DeleteFile(Dir + '\' + Name);
    until not FindNext(FR);
  finally
    FindClose(FR);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then CleanAppDir(ExpandConstant('{app}'));
end;
