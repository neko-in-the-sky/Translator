<#
.SYNOPSIS
Builds the release payload: the portable zip and the installer, both from one publish folder.

.DESCRIPTION
The release workflow and the PR installer job both call this script, so a PR tests exactly what a
release ships. Runs on Windows PowerShell 5.1 and PowerShell 7. Needs Inno Setup 6 or 7.

.EXAMPLE
powershell -NoProfile -File build/Build-Release.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    # Relative paths are resolved against the repository root.
    [string] $OutputDir = 'artifacts',

    # Extra ISCC defines, without the /D, e.g. SimulateMissingWebView2.
    [string[]] $IsccDefines = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Expected a version like 1.0.0, got '$Version'"
}

$root = Split-Path $PSScriptRoot -Parent
$out = if ([IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $root $OutputDir }
$publish = Join-Path $out 'publish'
$zip = Join-Path $out "Translator-$Version-win-x64.zip"

function Find-Iscc {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) {
        throw "ISCC.exe was not found on PATH or in: $($candidates -join '; '). " +
            'Install Inno Setup with: winget install --id JRSoftware.InnoSetup -e (or: choco install innosetup)'
    }
    return $found
}

# Before the slow publish, so that a missing Inno Setup fails in seconds.
$iscc = Find-Iscc

foreach ($stale in @($publish, $zip)) {
    if (Test-Path $stale) {
        Remove-Item $stale -Recurse -Force
    }
}

# Do NOT add -p:IncludeNativeLibrariesForSelfExtract=true here. It silently drops Blocklist/ads.txt
# from the publish output (154k ad domains), which disables ad blocking at runtime with no error.
# Saves 9 MB, costs a feature.
dotnet publish (Join-Path $root 'Translator\Translator.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    "-p:Version=$Version" `
    -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# The app reads these from disk at runtime relative to AppContext.BaseDirectory. If any are missing
# the app still starts but silently loses blocklisting or page cleanup.
$required = @(
    'Translator.exe'
    'appsettings.json'
    'Blocklist/ads.txt'
    'Blocklist/my.txt'
    'Blocklist/tracking.txt'
    'js/oxford.js'
    'js/free_dictionary.js'
    'js/spanishdict.js'
    'templates/info_template.html'
)
$missing = @($required | Where-Object { -not (Test-Path (Join-Path $publish $_)) })
if ($missing.Count -gt 0) {
    throw "Missing from publish output: $($missing -join ', ')"
}

Remove-Item (Join-Path $publish '*.pdb')
Get-ChildItem $publish -Recurse -File | ForEach-Object {
    '{0,10:N0}  {1}' -f $_.Length, $_.FullName.Substring($publish.Length + 1)
}

# Entry names are built by hand because on Windows PowerShell 5.1 both Compress-Archive and
# ZipFile.CreateFromDirectory write them with backslashes, which are not valid zip separators.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $publish -Recurse -File | ForEach-Object {
        $entryName = $_.FullName.Substring($publish.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $_.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
}
'{0}: {1:N1} MB' -f (Split-Path $zip -Leaf), ((Get-Item $zip).Length / 1MB)

$isccArgs = @("/DAppVersion=$Version", "/DPublishDir=$publish", "/O$out", '/Q')
$isccArgs += $IsccDefines | ForEach-Object { "/D$_" }
& $iscc @isccArgs (Join-Path $root 'installer\Translator.iss')
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

$setup = Join-Path $out "Translator-$Version-win-x64-setup.exe"
'{0}: {1:N1} MB' -f (Split-Path $setup -Leaf), ((Get-Item $setup).Length / 1MB)
