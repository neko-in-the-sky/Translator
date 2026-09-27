<#
.SYNOPSIS
Smoke-tests the installer: silent install, checks, silent uninstall, checks.

.DESCRIPTION
Installs into a temporary folder for the current user, starts the installed Translator so that the
uninstaller has to close it, uninstalls, and checks that everything the installer created is gone
while the user's settings folder is kept. Runs on Windows PowerShell 5.1 and PowerShell 7.

Refuses to run if Translator is already installed for this user, because it would uninstall it.

.EXAMPLE
powershell -NoProfile -File build/Test-Installer.ps1 -Setup artifacts/Translator-0.0.0-win-x64-setup.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Setup,

    # Where the Setup and uninstall logs go.
    [string] $LogDir = $env:TEMP
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{27CEFC81-BB7D-4993-A1E4-D7AC625FF4BA}_is1'
$startMenuShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Translator.lnk'
# Not $env:TEMP: for a long user name Windows sets it to an 8.3 short path (C:\Users\RUNNER~1\...),
# which never equals the long paths that shortcuts and processes report.
$installDir = Join-Path $env:LOCALAPPDATA ('Temp\translator-smoke-' + [Guid]::NewGuid().ToString('N'))
$installedExe = Join-Path $installDir 'Translator.exe'
$settingsDir = Join-Path $env:APPDATA 'Translator'
$marker = Join-Path $settingsDir ('smoke-test-' + [Guid]::NewGuid().ToString('N') + '.txt')

function Assert-That([bool] $Condition, [string] $Message) {
    if (-not $Condition) {
        throw "Installer smoke test failed: $Message"
    }
    "  ok  $Message"
}

function Get-InstalledTranslator {
    Get-Process -Name 'Translator' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installedExe }
}

function Invoke-Silently([string] $FilePath, [string] $LogName, [string[]] $ExtraArgs = @()) {
    $log = Join-Path $LogDir $LogName
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$log`"") + $ExtraArgs
    # -Wait also waits for child processes. Setup and the uninstaller both hand over to a copy of
    # themselves, and the checks must not run until that copy has finished.
    $process = Start-Process -FilePath $FilePath -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "$(Split-Path $FilePath -Leaf) exited with code $($process.ExitCode); see $log"
    }
}

$resolvedSetup = (Resolve-Path $Setup).Path
if (Test-Path $uninstallKey) {
    throw "Translator is already installed for this user. Uninstall it before running the smoke test, which would otherwise remove it."
}
New-Item -ItemType Directory -Force $LogDir | Out-Null
$createdSettingsDir = -not (Test-Path $settingsDir)
New-Item -ItemType Directory -Force $settingsDir | Out-Null
Set-Content -Path $marker -Value 'Written by the installer smoke test'

try {
    "Installing $resolvedSetup into $installDir"
    Invoke-Silently $resolvedSetup 'translator-setup.log' @("/DIR=`"$installDir`"")

    foreach ($file in @('Translator.exe', 'appsettings.json', 'Blocklist\ads.txt', 'js\oxford.js', 'templates\info_template.html')) {
        Assert-That (Test-Path (Join-Path $installDir $file)) "installed $file"
    }
    Assert-That (Test-Path $uninstallKey) 'registered the uninstall entry'
    Assert-That (Test-Path $startMenuShortcut) 'created the Start menu shortcut'
    $target = (New-Object -ComObject WScript.Shell).CreateShortcut($startMenuShortcut).TargetPath
    Assert-That ($target -eq $installedExe) "the Start menu shortcut points to $installedExe (it points to $target)"

    # A running Translator must not stop the uninstaller from removing its files.
    Start-Process -FilePath $installedExe | Out-Null
    $deadline = (Get-Date).AddSeconds(15)
    while (@(Get-InstalledTranslator).Count -eq 0 -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    Assert-That (@(Get-InstalledTranslator).Count -gt 0) 'the installed Translator is running'

    # Stands in for WebView2's data folder, which the app creates and Setup doesn't know about.
    $webView2Dir = Join-Path $installDir 'Translator.exe.WebView2'
    New-Item -ItemType Directory -Force $webView2Dir | Out-Null
    Set-Content -Path (Join-Path $webView2Dir 'smoke.txt') -Value 'stand-in'

    'Uninstalling'
    Invoke-Silently (Join-Path $installDir 'unins000.exe') 'translator-uninstall.log'

    Assert-That (@(Get-InstalledTranslator).Count -eq 0) 'the uninstaller closed the installed Translator'
    Assert-That (-not (Test-Path $installDir)) 'removed the install folder, including the WebView2 data folder'
    Assert-That (-not (Test-Path $uninstallKey)) 'removed the uninstall entry'
    Assert-That (-not (Test-Path $startMenuShortcut)) 'removed the Start menu shortcut'
    Assert-That (Test-Path $marker) "kept the user's settings folder"
    'Installer smoke test passed'
}
finally {
    # Leave the machine as it was, even after a failed check.
    Get-InstalledTranslator | Stop-Process -Force
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (Test-Path $uninstaller) {
        Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait
    }
    if (Test-Path $installDir) {
        Remove-Item $installDir -Recurse -Force
    }
    Remove-Item $marker -ErrorAction SilentlyContinue
    if ($createdSettingsDir -and (Test-Path $settingsDir) -and -not (Get-ChildItem $settingsDir -Force)) {
        Remove-Item $settingsDir
    }
}
