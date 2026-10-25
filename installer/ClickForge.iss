; Compile only through scripts/Build-Installer.ps1: PayloadFiles is generated from
; the ZIP after the existing production package verifier has accepted it.
#ifndef AppVersion
  #error Missing verified application version
#endif
#ifndef InstallerId
  #define InstallerId "ClickForge.Desktop"
#endif
#ifndef DisplayName
  #define DisplayName "ClickForge"
#endif

[Setup]
AppId={#InstallerId}
AppName={#DisplayName}
AppVersion={#AppVersion}
AppVerName={#DisplayName} {#AppVersion}
AppPublisher=ClickForge
AppPublisherURL=https://github.com/ojeshfrrr/ClickForge
AppSupportURL=https://github.com/ojeshfrrr/ClickForge/issues
DefaultDirName={localappdata}\Programs\{#DisplayName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
DisableDirPage=auto
WizardStyle=modern
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\ClickForge.exe
UninstallDisplayName={#DisplayName}
OutputDir={#OutputDirectory}
OutputBaseFilename=ClickForge-Setup-v{#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoDescription=ClickForge per-user Setup
Compression=lzma2/normal
SolidCompression=yes
CloseApplications=no
RestartApplications=no
AppMutex=Local\ClickForge.SetupGuard
SetupMutex=Local\ClickForge.Setup
Uninstallable=yes
UninstallFilesDir={app}
UninstallLogging=yes

[Tasks]
Name: desktopicon; Description: "Create a &Desktop shortcut"; GroupDescription: "Shortcuts:"
Name: startmenuicon; Description: "Create a &Start menu shortcut"; GroupDescription: "Shortcuts:"

[Files]
#include PayloadFiles

[Run]
Filename: "{app}\ClickForge.exe"; WorkingDir: "{app}"; Description: "Launch {#DisplayName}"; Flags: nowait postinstall skipifsilent unchecked

[Messages]
SetupAppRunningError=ClickForge is running.%n%nPress its emergency-stop shortcut, then choose Exit ClickForge from its tray menu (or close it with close-to-tray disabled). Retry Setup after it exits. No process will be force-closed.
UninstallAppRunningError=ClickForge is running.%n%nPress its emergency-stop shortcut, then choose Exit ClickForge from its tray menu. Retry uninstall after it exits. Personal settings and profiles will be kept.

[Code]
const
  RegistrationKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#InstallerId}_is1';

function SamePath(const A, B: String): Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(ExpandFileName(A)), RemoveBackslashUnlessRoot(ExpandFileName(B))) = 0;
end;

function OwnedInstallation: Boolean;
var Identity, Location: String;
begin
  Result := RegQueryStringValue(HKCU, RegistrationKey, 'ClickForgeInstallerId', Identity) and
    (Identity = '{#InstallerId}') and RegQueryStringValue(HKCU, RegistrationKey, 'InstallLocation', Location) and
    SamePath(Location, ExpandConstant('{app}'));
end;

function ShortcutPath(Desktop: Boolean): String;
begin
  if Desktop then Result := ExpandConstant('{userdesktop}\{#DisplayName}.lnk')
  else Result := ExpandConstant('{userprograms}\{#DisplayName}\{#DisplayName}.lnk');
end;

function ShortcutPointsHere(const Path: String): Boolean;
var Shell, Link: Variant;
begin
  Result := False;
  if not FileExists(Path) then Exit;
  try
    Shell := CreateOleObject('WScript.Shell'); Link := Shell.CreateShortcut(Path);
    Result := SamePath(Link.TargetPath, ExpandConstant('{app}\ClickForge.exe'));
  except
    Result := False;
  end;
end;

function DirectoryHasFiles: Boolean;
var Item: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{app}\*'), Item) then
  begin
    try
      repeat
        if (Item.Name <> '.') and (Item.Name <> '..') then Result := True;
      until Result or not FindNext(Item);
    finally FindClose(Item); end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var MS, LS: Cardinal; Identity, Location: String;
begin
  Result := '';
  if CheckForMutexes('Local\ClickForge.SetupGuard') then
  begin Result := 'Exit ClickForge from its tray menu, then retry. Setup never force-closes processes.'; Exit; end;
  if RegQueryStringValue(HKCU, RegistrationKey, 'ClickForgeInstallerId', Identity) and
     (Identity = '{#InstallerId}') and RegQueryStringValue(HKCU, RegistrationKey, 'InstallLocation', Location) and
     not SamePath(Location, ExpandConstant('{app}')) then
  begin Result := 'This ClickForge installation already uses another folder. Use its existing location, or uninstall before changing folders. Personal settings and profiles are preserved.'; Exit; end;
  if DirectoryHasFiles and not OwnedInstallation then
  begin Result := 'This folder contains files that do not belong to this ClickForge installer. Choose an empty folder. Portable copies are not overwritten or moved.'; Exit; end;
  if OwnedInstallation and GetVersionNumbers(ExpandConstant('{app}\ClickForge.exe'), MS, LS) then
    if (MS > {#VersionMS}) or ((MS = {#VersionMS}) and (LS > {#VersionLS})) then
    begin Result := 'A newer ClickForge version is installed. This Setup will not downgrade it.'; Exit; end;
  if WizardIsTaskSelected('desktopicon') and FileExists(ShortcutPath(True)) and not ShortcutPointsHere(ShortcutPath(True)) then
  begin Result := 'A different Desktop shortcut already has this name. Uncheck Desktop shortcut or rename that shortcut before continuing.'; Exit; end;
  if WizardIsTaskSelected('startmenuicon') and FileExists(ShortcutPath(False)) and not ShortcutPointsHere(ShortcutPath(False)) then
    Result := 'A different Start menu shortcut already has this name. Uncheck Start menu shortcut or rename it before continuing.';
end;

procedure ManageShortcut(Desktop, Selected: Boolean);
var Path, ValueName: String; Shell, Link: Variant;
begin
  Path := ShortcutPath(Desktop);
  if Desktop then ValueName := 'ClickForgeDesktopShortcut' else ValueName := 'ClickForgeStartShortcut';
  if Selected then
  begin
    if not ForceDirectories(ExtractFileDir(Path)) then RaiseException('Could not create the shortcut folder.');
    Shell := CreateOleObject('WScript.Shell'); Link := Shell.CreateShortcut(Path);
    Link.TargetPath := ExpandConstant('{app}\ClickForge.exe');
    Link.WorkingDirectory := ExpandConstant('{app}');
    Link.IconLocation := ExpandConstant('{app}\ClickForge.exe,0');
    Link.Description := 'ClickForge desktop click control'; Link.Save;
    if not RegWriteStringValue(HKCU, RegistrationKey, ValueName, Path) then RaiseException('Could not record shortcut ownership.');
  end
  else
  begin
    if ShortcutPointsHere(Path) and not DeleteFile(Path) then RaiseException('Could not remove the previously installed shortcut.');
    RegDeleteValue(HKCU, RegistrationKey, ValueName);
    if not Desktop then RemoveDir(ExtractFileDir(Path));
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { Inno recreates its uninstall registration after processing [Registry].
      Write ownership only after that registration has been committed. }
    if not RegWriteStringValue(HKCU, RegistrationKey, 'ClickForgeInstallerId', '{#InstallerId}') then
      RaiseException('Could not record ClickForge installation ownership.');
    ManageShortcut(True, WizardIsTaskSelected('desktopicon'));
    ManageShortcut(False, WizardIsTaskSelected('startmenuicon'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Path: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Shortcuts are deliberately not tracked as blind [Icons] deletions: remove
      only a recorded link that still targets this exact installation. }
    if RegQueryStringValue(HKCU, RegistrationKey, 'ClickForgeDesktopShortcut', Path) and
       SamePath(Path, ShortcutPath(True)) and ShortcutPointsHere(Path) then DeleteFile(Path);
    if RegQueryStringValue(HKCU, RegistrationKey, 'ClickForgeStartShortcut', Path) and
       SamePath(Path, ShortcutPath(False)) and ShortcutPointsHere(Path) then
    begin DeleteFile(Path); RemoveDir(ExtractFileDir(Path)); end;
    { No user-data or Updates directory is removed, including recovery backups. }
  end;
end;
