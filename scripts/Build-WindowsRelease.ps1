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
$packageRoot = Join-Path $artifactsRoot "package"
$installerInputDir = Join-Path $packageRoot "installer-input"
$zipPath = Join-Path $artifactsRoot "VeloCenter-$version-$RuntimeIdentifier.zip"
$payloadArchivePath = Join-Path $installerInputDir "VeloCenter-Payload.zip"
$installerPath = Join-Path $artifactsRoot "VeloCenter-$version-$RuntimeIdentifier-setup.exe"
$sedPath = Join-Path $packageRoot "VeloCenter-$version.sed"
$artifactsBuildRoot = Join-Path $repoRootPath ".artifacts\\build\\"

New-Item -ItemType Directory -Force -Path $artifactsRoot, $packageRoot | Out-Null
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
if (Test-Path -LiteralPath $installerInputDir) {
    Remove-Item -LiteralPath $installerInputDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $installerPath) {
    Remove-Item -LiteralPath $installerPath -Force
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

New-Item -ItemType Directory -Force -Path $installerInputDir | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRootPath "packaging\\windows\\Install-VeloCenter.ps1") -Destination $installerInputDir -Force
Copy-Item -LiteralPath (Join-Path $repoRootPath "packaging\\windows\\Install-VeloCenter.cmd") -Destination $installerInputDir -Force

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
Copy-Item -LiteralPath $zipPath -Destination $payloadArchivePath -Force

$files = Get-ChildItem -LiteralPath $installerInputDir -Recurse -File | Sort-Object FullName
$groups = $files | Group-Object DirectoryName
$sourceFilesEntries = New-Object System.Collections.Generic.List[string]
$sourceSections = New-Object System.Collections.Generic.List[string]
$stringsEntries = New-Object System.Collections.Generic.List[string]
$fileIndex = 0
$sectionIndex = 0

foreach ($group in $groups) {
    $sectionName = "SourceFiles$sectionIndex"
    $sourceFilesEntries.Add("$sectionName=$($group.Name)")
    $sectionLines = New-Object System.Collections.Generic.List[string]
    $sectionLines.Add("[$sectionName]")

    foreach ($file in ($group.Group | Sort-Object Name)) {
        $stringKey = "FILE$fileIndex"
        $stringsEntries.Add("$stringKey=""$($file.Name)""")
        $sectionLines.Add("%$stringKey%=")
        $fileIndex++
    }

    $sourceSections.Add(($sectionLines -join [Environment]::NewLine))
    $sectionIndex++
}

$sedLines = @(
    "[Version]"
    "Class=IEXPRESS"
    "SEDVersion=3"
    "[Options]"
    "PackagePurpose=InstallApp"
    "ShowInstallProgramWindow=0"
    "HideExtractAnimation=1"
    "UseLongFileName=1"
    "InsideCompressed=0"
    "CAB_FixedSize=0"
    "CAB_ResvCodeSigning=0"
    "RebootMode=N"
    "InstallPrompt=%InstallPrompt%"
    "DisplayLicense=%DisplayLicense%"
    "FinishMessage=%FinishMessage%"
    "TargetName=%TargetName%"
    "FriendlyName=%FriendlyName%"
    "AppLaunched=%AppLaunched%"
    "PostInstallCmd=%PostInstallCmd%"
    "AdminQuietInstCmd=%AdminQuietInstCmd%"
    "UserQuietInstCmd=%UserQuietInstCmd%"
    "SourceFiles=SourceFiles"
    "[Strings]"
    "InstallPrompt="
    "DisplayLicense="
    "FinishMessage=VeloCenter $version has been installed."
    "TargetName=$installerPath"
    "FriendlyName=VeloCenter $version"
    "AppLaunched=cmd.exe /c Install-VeloCenter.cmd"
    "PostInstallCmd=<None>"
    "AdminQuietInstCmd=cmd.exe /c Install-VeloCenter.cmd"
    "UserQuietInstCmd=cmd.exe /c Install-VeloCenter.cmd"
)

$sedLines += $stringsEntries
$sedLines += "[SourceFiles]"
$sedLines += $sourceFilesEntries
$sedLines += $sourceSections

Set-Content -LiteralPath $sedPath -Value $sedLines -Encoding ASCII

& iexpress.exe /N $sedPath | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "iexpress failed with exit code $LASTEXITCODE."
}

[pscustomobject]@{
    Version = $version
    PublishDirectory = $publishDir
    ZipPackage = $zipPath
    Installer = $installerPath
}
