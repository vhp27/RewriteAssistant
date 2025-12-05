; Inno Setup Script for Rewrite Assistant
; This script creates a single-file Windows installer that bundles the WPF frontend,
; Node.js backend, and all resources into one setup executable.

[Setup]
; App metadata
AppName=Rewrite Assistant
AppVersion=1.0.0
AppPublisher=Rewrite Assistant Team
AppPublisherURL=https://github.com/yourusername/rewrite-assistant
AppSupportURL=https://github.com/yourusername/rewrite-assistant/issues
AppUpdatesURL=https://github.com/yourusername/rewrite-assistant/releases
DefaultDirName={autopf}\RewriteAssistant
DefaultGroupName=Rewrite Assistant
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=RewriteAssistantSetup
SetupIconFile=src\RewriteAssistant\Resources\app.ico
Compression=lzma2
SolidCompression=yes
; Installer appearance
WizardStyle=modern
DisableProgramGroupPage=yes
; Add/Remove Programs registration
UninstallDisplayIcon={app}\RewriteAssistant.exe
UninstallDisplayName=Rewrite Assistant
; Architecture
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; Privileges
PrivilegesRequired=admin
; Close running application before install/uninstall
CloseApplications=yes
CloseApplicationsFilter=RewriteAssistant.exe,backend.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application executable (self-contained WPF frontend)
Source: "staging\RewriteAssistant.exe"; DestDir: "{app}"; Flags: ignoreversion
; Backend executable (pkg-compiled Node.js backend)
Source: "staging\backend.exe"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Add any additional resource files here if needed in the future

[Icons]
; Start Menu shortcut
Name: "{group}\Rewrite Assistant"; Filename: "{app}\RewriteAssistant.exe"; IconFilename: "{app}\RewriteAssistant.exe"
; Desktop shortcut (optional, based on user selection)
Name: "{autodesktop}\Rewrite Assistant"; Filename: "{app}\RewriteAssistant.exe"; IconFilename: "{app}\RewriteAssistant.exe"; Tasks: desktopicon

[Run]
; Option to launch the application after installation
Filename: "{app}\RewriteAssistant.exe"; Description: "{cm:LaunchProgram,Rewrite Assistant}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill any running instances before uninstall
Filename: "taskkill"; Parameters: "/F /IM RewriteAssistant.exe"; Flags: runhidden; RunOnceId: "KillApp"
Filename: "taskkill"; Parameters: "/F /IM backend.exe"; Flags: runhidden; RunOnceId: "KillBackend"

[UninstallDelete]
; Clean up any files created at runtime
Type: filesandordirs; Name: "{app}"
; Clean up user config directory
Type: filesandordirs; Name: "{userappdata}\RewriteAssistant"

[InstallDelete]
; Clean up before reinstall
Type: filesandordirs; Name: "{app}"
