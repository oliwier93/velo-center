[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string[]]$Assets,

    [string]$Repository = "oliwier93/velo-center",
    [string]$ReleaseNotesPath,
    [string]$TargetCommitish = "main"
)

$ErrorActionPreference = "Stop"

$token = if ($env:GITHUB_TOKEN) {
    $env:GITHUB_TOKEN
} elseif ($env:GH_TOKEN) {
    $env:GH_TOKEN
} else {
    throw "Set GITHUB_TOKEN or GH_TOKEN before publishing a GitHub release."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedAssets = foreach ($asset in $Assets) {
    (Resolve-Path (Join-Path $repoRoot.Path $asset)).Path
}

if ([string]::IsNullOrWhiteSpace($ReleaseNotesPath)) {
    $ReleaseNotesPath = ".\\docs\\release-notes\\$Version.md"
}

$resolvedNotesPath = Resolve-Path (Join-Path $repoRoot.Path $ReleaseNotesPath)

$tagName = "v$Version"
$releaseName = "VeloCenter $Version"
$releaseBody = Get-Content -Raw -LiteralPath $resolvedNotesPath.Path

$headers = @{
    Accept               = "application/vnd.github+json"
    Authorization        = "Bearer $token"
    "X-GitHub-Api-Version" = "2022-11-28"
    "User-Agent"         = "VeloCenterReleaseScript"
}

$releasePayload = @{
    tag_name         = $tagName
    target_commitish = $TargetCommitish
    name             = $releaseName
    body             = $releaseBody
    draft            = $false
    prerelease       = $true
} | ConvertTo-Json -Depth 5

$release = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$Repository/releases" -Headers $headers -Body $releasePayload -ContentType "application/json"
$uploadBaseUrl = ($release.upload_url -replace "\{\?name,label\}$", "")

foreach ($assetPath in $resolvedAssets) {
    $assetName = Split-Path -Leaf $assetPath
    $uploadUrl = "$uploadBaseUrl?name=$([uri]::EscapeDataString($assetName))"

    Invoke-RestMethod `
        -Method Post `
        -Uri $uploadUrl `
        -Headers $headers `
        -ContentType "application/octet-stream" `
        -InFile $assetPath | Out-Null
}

[pscustomobject]@{
    Release = $release.html_url
    Tag = $tagName
    Assets = $resolvedAssets
}
