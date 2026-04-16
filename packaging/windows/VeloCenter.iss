#define MyAppName "VeloCenter"
#define MyAppPublisher "oliwier93"
#define MyAppURL "https://github.com/oliwier93/velo-center"
#define MyAppExeName "VeloCenter.App.exe"

#ifndef MyAppVersion
  #define MyAppVersion "0.0.1-alpha"
#endif

#ifndef MySourceDir
  #error MySourceDir must be defined.
#endif

#ifndef MyOutputDir
  #error MyOutputDir must be defined.
#endif

#ifndef MySetupIconFile
  #error MySetupIconFile must be defined.
#endif

[Setup]
AppId={{8D4D6F8D-8D25-4B41-9B6B-B0E76C4A2B79}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\VeloCenter
DefaultGroupName=VeloCenter
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=no
SetupIconFile={#MySetupIconFile}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir={#MyOutputDir}
OutputBaseFilename=VeloCenter-{#MyAppVersion}-win-x64-setup
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\VeloCenter"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\VeloCenter"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Uruchom VeloCenter"; Flags: nowait postinstall skipifsilent
