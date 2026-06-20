; Quillora - Inno Setup Script
; Requires .NET 8.0 Desktop Runtime

#define AppName "Quillora"
#define AppVersion "1.0.0"
#define AppPublisher "WenMingMing"
#define AppExeName "WpfMarkdownEditor.Sample.exe"
#define AppCopyright "Copyright (c) 2026 WenMingMing"
; Target CPU architecture for this build. Override from the command line with
; ISCC /DArch=arm64 (defaults to x64). The #ifndef guard lets /D win over the
; in-file default, per the ISPP override rules.
#ifndef Arch
  #define Arch "x64"
#endif
; Map the .NET RID-style arch to the Inno Setup Architectures* token. arm64 is
; only honored by Inno Setup 6.3+, so an arm64 build requires that upgrade;
; the script itself is already written to accept it.
#if Arch == "arm64"
  #define ArchAllowed "arm64"
#else
  #define ArchAllowed "x64"
#endif
; Previous product identity ("Markdown Viewer"). Kept only to detect and
; cleanly uninstall legacy builds during a Quillora install. The braces are
; part of the value because the Uninstall registry subkey is literally
; "{GUID}_is1" and FindLegacyUninstaller concatenates this constant verbatim.
#define LegacyAppId "{B8E3F1A2-4D5C-6E7F-8A9B-0C1D2E3F4A5B}"
#define LegacyAppName "Markdown Viewer"

[Setup]
; Fresh AppId so Quillora is treated as a distinct product from the legacy
; "Markdown Viewer" builds. The old installer reused the previous GUID, which
; made Inno Setup inherit the old install dir (e.g. D:\Markdown Viewer).
; The legacy AppId is still referenced below to detect and uninstall it.
AppId={{A4726761-4487-41A2-84C4-BE8E647EBF09}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/WenElevating/wpf-markdown-viewer
AppSupportURL=https://github.com/WenElevating/wpf-markdown-viewer/issues
AppCopyright={#AppCopyright}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
; In per-user mode (PrivilegesRequired=lowest) with {autopf}, Inno Setup would
; auto-hide the destination folder page. Force it on so users can still pick a
; folder inside their writable profile (or anywhere they have write access).
DisableDirPage=no
; Reuse the previous install directory when upgrading the same AppId (Quillora
; over Quillora). This only matches "same application" installs, so the legacy
; Markdown Viewer (different AppId) is never picked up here.
UsePreviousAppDir=yes
AllowNoIcons=yes
OutputDir=installer-output
OutputBaseFilename=Quillora-{#AppVersion}-{#Arch}-Setup
SetupIconFile=assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0.17763
; Tell Inno Setup that this installer registers file associations so it can
; call SHChangeNotify(SHCNE_ASSOCCHANGED) after setup/uninstall finishes.
; This refreshes the shell icon and "Open with" caches without a logoff.
ChangesAssociations=yes

; Uninstall info
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "fileassoc"; Description: "{cm:AssociateMarkdownFiles,{#AppName}}"; GroupDescription: "{cm:FileAssociationsGroup}"

[CustomMessages]
; English (default). The prefix must match the Name: in [Languages].
english.AssociateMarkdownFiles=Associate .md files with %1
english.FileAssociationsGroup=File Associations:
; Simplified Chinese
chinesesimplified.AssociateMarkdownFiles=将 .md 文件关联到 %1（加入"打开方式"列表）
chinesesimplified.FileAssociationsGroup=文件关联:

[Files]
Source: "samples\WpfMarkdownEditor.Sample\bin\Release\net8.0-windows\publish\win-{#Arch}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Register .md into the "Open with" list without taking over the default.
; OpenWithProgids uses a REG_NONE value (not REG_SZ) so Windows treats it as a
; ProgID reference rather than a string; this is the documented way to surface
; an app in the "Open with" menu without claiming the default program.
Root: HKA; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: none; ValueName: "Quillora.md"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Quillora.md"; ValueType: string; ValueName: ""; ValueData: "Markdown File"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Quillora.md\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Quillora.md\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc

[Code]
const
  // Legacy "Markdown Viewer" AppId, used to detect and uninstall old builds.
  LegacyAppId = '{#LegacyAppId}';

var
  LegacyUninstaller: String;
  LegacyDetected: Boolean;

function IsDotNetInstalled: Boolean;
var
  ResultCode: Integer;
begin
  // Check for .NET 8 Desktop Runtime
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  if not Result then
  begin
    if MsgBox('.NET 8.0 Desktop Runtime is required but not found.' + #13#10 + #13#10 +
      'Would you like to download it now?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
    Result := True; // Allow installation to continue
  end;
end;

// Look up the legacy install's UninstallString from any of the four registry
// hives/views Inno Setup could have written it to (per-user vs per-machine,
// 32-bit vs 64-bit). Returns the path with surrounding quotes stripped.
function FindLegacyUninstaller(var Path: String): Boolean;
var
  Subkey: String;
begin
  Subkey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + LegacyAppId + '_is1';
  Result :=
    RegQueryStringValue(HKLM64, Subkey, 'UninstallString', Path) or
    RegQueryStringValue(HKLM32, Subkey, 'UninstallString', Path) or
    RegQueryStringValue(HKCU64, Subkey, 'UninstallString', Path) or
    RegQueryStringValue(HKCU32, Subkey, 'UninstallString', Path);
  if Result and (Length(Path) > 0) then
  begin
    // Inno writes the value quoted, e.g. "D:\Markdown Viewer\unins000.exe".
    Path := RemoveQuotes(Path);
  end;
end;

// Remove leftover legacy uninstall registry keys across all four views. Used
// when the legacy unins000.exe is missing and cannot clean up itself.
procedure DeleteLegacyUninstallKeys;
var
  Subkey: String;
begin
  Subkey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + LegacyAppId + '_is1';
  RegDeleteKeyIncludingSubkeys(HKLM64, Subkey);
  RegDeleteKeyIncludingSubkeys(HKLM32, Subkey);
  RegDeleteKeyIncludingSubkeys(HKCU64, Subkey);
  RegDeleteKeyIncludingSubkeys(HKCU32, Subkey);
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled;
  if not Result then
    Exit;

  // Detect a legacy "Markdown Viewer" install so we can offer to remove it
  // before copying files. We only record it here; the actual uninstall runs in
  // PrepareToInstall so the wizard has finished and files are not in use.
  LegacyDetected := FindLegacyUninstaller(LegacyUninstaller);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  LegacyGroup: String;
begin
  Result := '';
  if not LegacyDetected then
    Exit;

  if MsgBox('{#LegacyAppName} was detected on this computer.' + #13#10 + #13#10 +
      '{#AppName} replaces it. Do you want to uninstall {#LegacyAppName} now?',
      mbConfirmation, MB_YESNO) <> IDYES then
    Exit;

  if FileExists(LegacyUninstaller) then
  begin
    // Normal case: the legacy uninstaller is still there, run it silently.
    // /NORESTART keeps this machine from rebooting mid-setup; the exit code is
    // ignored so a quirky legacy uninstall cannot block the Quillora install.
    Exec(LegacyUninstaller, '/SILENT /NORESTART', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
  end
  else
  begin
    // Fallback: the install dir was deleted but registry/start-menu leftovers
    // remain. Clear them manually so the "Markdown Viewer detected" prompt
    // does not keep reappearing on every future Quillora install.
    DeleteLegacyUninstallKeys;
    LegacyGroup := ExpandConstant('{userprograms}\{#LegacyAppName}');
    if DirExists(LegacyGroup) then
      DelTree(LegacyGroup, True, True, True);
  end;
end;
