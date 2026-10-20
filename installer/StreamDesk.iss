; Script de Inno Setup para StreamDesk IPTV
; Versión Autocontenida (Windows x64)

#define MyAppName "StreamDesk IPTV"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "eduardour15"
#define MyAppExeName "StreamDesk.Probe.Desktop.exe"

[Setup]
AppId={{B255FA3E-C634-44DE-9293-1959C5C158A3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\StreamDesk
DefaultGroupName=StreamDesk
AllowNoIcons=yes
LicenseFile=license.txt
SetupIconFile=..\src\StreamDesk.Probe.Desktop\Resources\StreamDesk.ico
OutputDir=..\artifacts\installer
OutputBaseFilename=StreamDesk_Installer
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Archivos principales de la aplicación autocontenida (excluyendo arquitecturas no x64 para optimizar tamaño)
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "libvlc\win-arm64\*,libvlc\win-arm64,libvlc\win-x86\*,libvlc\win-x86"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
