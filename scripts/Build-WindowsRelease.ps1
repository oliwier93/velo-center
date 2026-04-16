[CmdletBinding()]
param(
    [string]$RuntimeIdentifier = "win-x64",
    [string]$ProjectPath = ".\\src\\VeloCenter.App\\VeloCenter.App.csproj"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoRootPath = $repoRoot.Path
$propsPath = Join-Path $repoRootPath "Directory.Build.props"

if (-not (Test-Path -LiteralPath $propsPath)) {
    throw "Missing version file: $propsPath"
}

[xml]$propsXml = Get-Content -Raw -LiteralPath $propsPath
$versionPrefix = $propsXml.SelectSingleNode("/Project/PropertyGroup/VersionPrefix").InnerText.Trim()
$versionSuffixNode = $propsXml.SelectSingleNode("/Project/PropertyGroup/VersionSuffix")
$versionSuffix = if ($null -eq $versionSuffixNode) { "" } else { $versionSuffixNode.InnerText.Trim() }
$version = if ([string]::IsNullOrWhiteSpace($versionSuffix)) {
    $versionPrefix
} else {
    "$versionPrefix-$versionSuffix"
}

$resolvedProjectPath = Resolve-Path (Join-Path $repoRootPath $ProjectPath)
$artifactsRoot = Join-Path $repoRootPath ".artifacts\\release"
$publishDir = Join-Path $artifactsRoot "publish\\$RuntimeIdentifier"
$zipPath = Join-Path $artifactsRoot "VeloCenter-$version-$RuntimeIdentifier.zip"
$installerPath = Join-Path $artifactsRoot "VeloCenter-$version-$RuntimeIdentifier-setup.exe"
$artifactsBuildRoot = Join-Path $repoRootPath ".artifacts\\build\\"
$issPath = Resolve-Path (Join-Path $repoRootPath "packaging\\windows\\VeloCenter.iss")
$iconPath = Resolve-Path (Join-Path $repoRootPath "src\\VeloCenter.App\\Assets\\avalonia-logo.ico")
$isccCommand = Get-Command iscc.exe, iscc -ErrorAction SilentlyContinue | Select-Object -First 1

if ($null -eq $isccCommand) {
    $commonIsccPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )

    foreach ($candidatePath in $commonIsccPaths) {
        if (Test-Path -LiteralPath $candidatePath) {
            $isccCommand = [pscustomobject]@{ Source = $candidatePath }
            break
        }
    }
}

if ($null -eq $isccCommand) {
    throw "Inno Setup Compiler (iscc) was not found. Install Inno Setup before building the Windows installer."
}

New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

foreach ($existingArtifact in (Get-ChildItem -LiteralPath $artifactsRoot -File -Filter "VeloCenter-*.zip" -ErrorAction SilentlyContinue)) {
    Remove-Item -LiteralPath $existingArtifact.FullName -Force
}

foreach ($existingInstaller in (Get-ChildItem -LiteralPath $artifactsRoot -File -Filter "VeloCenter-*-setup.exe" -ErrorAction SilentlyContinue)) {
    Remove-Item -LiteralPath $existingInstaller.FullName -Force
}

$publishArgs = @(
    "publish"
    $resolvedProjectPath.Path
    "-c"
    "Release"
    "-r"
    $RuntimeIdentifier
    "--self-contained"
    "true"
    "--disable-build-servers"
    "-o"
    $publishDir
    "-p:Version=$version"
    "-p:UseArtifactsOutput=true"
    "-p:ArtifactsRoot=$artifactsBuildRoot"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$isccArgs = @(
    "/DMyAppVersion=$version"
    "/DMySourceDir=$publishDir"
    "/DMyOutputDir=$artifactsRoot"
    "/DMySetupIconFile=$($iconPath.Path)"
    $issPath.Path
)

& $isccCommand.Source @isccArgs | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "iscc failed with exit code $LASTEXITCODE."
}

[pscustomobject]@{
    Version = $version
    PublishDirectory = $publishDir
    ZipPackage = $zipPath
    Installer = $installerPath
}
