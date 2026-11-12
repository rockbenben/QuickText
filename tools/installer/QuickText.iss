#define AppName "QuickText"
#ifndef AppVersion
  #error AppVersion is required
#endif
[Setup]
AppId={{E8125AE1-BB4A-441C-8EB6-C098323E0914}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\..\assets\branding\quicktext.ico
ShowLanguageDialog=no
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#AppName}.exe
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional tasks:"
[Files]
Source: "..\..\src\QuickText.App\bin\Release\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppName}.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppName}.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\{#AppName}.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
[UninstallDelete]
Type: dirifempty; Name: "{app}"
