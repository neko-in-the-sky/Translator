; Translator's installer. Build it with build/Build-Release.ps1, which passes the version and the
; publish folder, so that the installer and the zip contain the same files.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif

[Setup]
; Windows recognises an upgrade by this id. Never change it.
AppId={{27CEFC81-BB7D-4993-A1E4-D7AC625FF4BA}
AppName=Translator
AppVersion={#AppVersion}
AppVerName=Translator {#AppVersion}
AppPublisher=Neko in the Sky
AppPublisherURL=https://github.com/neko-in-the-sky/Translator
AppSupportURL=https://github.com/neko-in-the-sky/Translator/issues
VersionInfoVersion={#AppVersion}
; Per-user, into %LOCALAPPDATA%\Programs. The app writes next to its exe (WebView2's data folder),
; which a Program Files install could not do without admin rights.
PrivilegesRequired=lowest
DefaultDirName={autopf}\Translator
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Matches the app's target framework, net8.0-windows10.0.17763.0.
MinVersion=10.0.17763
SetupIconFile=..\Translator\icons\stack-of-books.ico
UninstallDisplayIcon={app}\Translator.exe
UninstallDisplayName=Translator
OutputBaseFilename=Translator-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
; Translator doesn't register with Restart Manager. "Launch Translator" on the last page starts it.
RestartApplications=no

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Translator"; Filename: "{app}\Translator.exe"

[UninstallDelete]
; The app creates WebView2's data folder, not Setup, so the uninstaller would otherwise leave it behind.
Type: filesandordirs; Name: "{app}\Translator.exe.WebView2"

[CustomMessages]
CloseTranslatorOnUninstall=Translator is running and will be closed so that it can be uninstalled.

[Code]
// Setup closes a running Translator through Restart Manager, but the uninstaller doesn't: it would
// leave the program files locked. So the uninstaller ends Translator itself. It keeps no unsaved
// state. Only a Translator started from this install folder is matched, so a copy unzipped
// elsewhere keeps running.

function RunningTranslators: Variant;
var
  Locator, Service: Variant;
  ExePath: String;
begin
  ExePath := ExpandConstant('{app}\Translator.exe');
  // WQL string literals escape backslashes and quotes with a backslash.
  StringChangeEx(ExePath, '\', '\\', True);
  StringChangeEx(ExePath, '''', '\''', True);
  Locator := CreateOleObject('WbemScripting.SWbemLocator');
  Service := Locator.ConnectServer('.', 'root\CIMV2');
  Result := Service.ExecQuery('SELECT * FROM Win32_Process WHERE ExecutablePath = ''' + ExePath + '''');
end;

function IsTranslatorRunning: Boolean;
begin
  try
    Result := RunningTranslators.Count > 0;
  except
    // Without WMI, fall back to Inno's default: files still in use are left behind.
    Log('Could not check whether Translator is running: ' + GetExceptionMessage);
    Result := False;
  end;
end;

procedure CloseTranslator;
var
  Processes, Process: Variant;
  I, Attempt: Integer;
begin
  try
    Processes := RunningTranslators;
    for I := 0 to Processes.Count - 1 do
    begin
      Process := Processes.ItemIndex(I);
      Process.Terminate();
    end;
  except
    Log('Could not close Translator: ' + GetExceptionMessage);
  end;

  // Terminate returns before the process has gone and released its files.
  for Attempt := 1 to 50 do
  begin
    if not IsTranslatorRunning then
      Exit;
    Sleep(100);
  end;
  Log('Translator is still running after being closed');
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  if IsTranslatorRunning then
  begin
    if not UninstallSilent then
      Result := MsgBox(CustomMessage('CloseTranslatorOnUninstall'), mbInformation, MB_OKCANCEL) = IDOK;
    if Result then
      CloseTranslator;
  end;
end;
