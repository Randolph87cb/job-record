#define AppId "E9B30C18-4D6D-40A5-A45A-0F0C4A1C6372"
#define AppName "工作时间记录"
#define AppPublisher "Randolph87cb"
#define AppExeName "JobRecord.App.exe"
#define AppDescription "Windows 悬浮工作时间记录工具"

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef InstallerVersion
  #define InstallerVersion "0.1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#ifndef IconFile
  #define IconFile "..\assets\branding\JobRecord.ico"
#endif

[Setup]
AppId={{{#AppId}}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/Randolph87cb/job-record
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=JobRecord-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExeName}
SetupLogging=yes
SetupIconFile={#IconFile}
VersionInfoVersion={#InstallerVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppDescription}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#InstallerVersion}

[Languages]
Name: "default"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务"; Flags: unchecked
Name: "autorun"; Description: "开机自动启动"; GroupDescription: "附加任务"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Tasks: autorun; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if not WizardIsTaskSelected('autorun') then begin
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
    end;
  end;
end;
