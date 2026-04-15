[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$payloadArchive = Join-Path $scriptRoot "VeloCenter-Payload.zip"
$stagingRoot = Join-Path $env:TEMP "VeloCenter-Installer-Staging"
$installRoot = Join-Path $env:LOCALAPPDATA "Programs\\VeloCenter"
$startMenuShortcut = Join-Path $env:APPDATA "Microsoft\\Windows\\Start Menu\\Programs\\VeloCenter.lnk"
$exePath = Join-Path $installRoot "VeloCenter.App.exe"

if (-not (Test-Path -LiteralPath $payloadArchive)) {
    throw "Installer payload archive was not found."
}

$runningProcess = Get-Process -Name "VeloCenter.App", "VeloCenter" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -ne $runningProcess) {
    throw "Close VeloCenter before installing or updating the application."
}

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

Expand-Archive -LiteralPath $payloadArchive -DestinationPath $stagingRoot -Force

$robocopyArgs = @(
    $stagingRoot
    $installRoot
    "/MIR"
    "/NFL"
    "/NDL"
    "/NJH"
    "/NJS"
    "/NC"
    "/NS"
)

& robocopy @robocopyArgs | Out-Null
if ($LASTEXITCODE -gt 7) {
    throw "robocopy failed with exit code $LASTEXITCODE."
}

$wshShell = New-Object -ComObject WScript.Shell
$shortcut = $wshShell.CreateShortcut($startMenuShortcut)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installRoot
$shortcut.IconLocation = "$exePath,0"
$shortcut.Save()

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

Write-Host "VeloCenter has been installed to $installRoot"
