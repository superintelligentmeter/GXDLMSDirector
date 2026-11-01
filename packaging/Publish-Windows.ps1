# GPL-2.0-only. Added 2026-11-01: reproducible Release packaging for the community fork.
[CmdletBinding()]
param([switch]$DisableDashboards)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$uiRepo = Join-Path (Split-Path $repo -Parent) 'Gurux.DLMS.UI.Net'
if (-not (Test-Path (Join-Path $uiRepo 'development/Gurux.DLMS.UI.Net.csproj'))) {
    throw 'Clone Gurux.DLMS.UI.Net beside GXDLMSDirector before publishing.'
}
function Get-SourceRevision([string]$Path) {
    $commit = & git -C $Path rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw "Cannot identify source revision: $Path" }
    $status = & git -C $Path status --porcelain
    if ($LASTEXITCODE -ne 0) { throw "Cannot check source state: $Path" }
    if ($status) { throw "Commit or isolate pending changes before packaging: $Path" }
    return $commit
}
$directorCommit = Get-SourceRevision $repo
$uiCommit = Get-SourceRevision $uiRepo
$variant = if ($DisableDashboards) { 'without-dashboards' } else { 'with-dashboards' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $PSScriptRoot "artifacts/$stamp-$variant"
$publish = Join-Path $output 'app'
$symbols = Join-Path $output 'symbols'
New-Item -ItemType Directory -Path $publish, $symbols | Out-Null
Push-Location $repo
try {
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'The .NET SDK is unavailable.' }
    $enabled = (-not $DisableDashboards).ToString().ToLowerInvariant()
    $arguments = @('publish', 'Development/GXDLMSDirector46.csproj', '-c', 'Release',
        '-p:PublishProfile=WindowsX64', "-p:EnableDashboards=$enabled", '-o', $publish, '-v:minimal')
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }

    foreach ($pdb in Get-ChildItem -LiteralPath $publish -Recurse -File -Filter '*.pdb') {
        $destination = Join-Path $symbols ([IO.Path]::GetRelativePath($publish, $pdb.FullName))
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $pdb.FullName -Destination $destination
        Remove-Item -LiteralPath $pdb.FullName
    }
    Copy-Item LICENSE, THIRD-PARTY-NOTICES, ASSET-PROVENANCE, CREDITS, MODIFICATIONS -Destination $publish
    Copy-Item packaging/README.md (Join-Path $publish 'BUILD-AND-RELEASE.md')

    # Locate the exact runtime packages selected by restore, including custom package caches.
    $assets = Get-Content Development/obj/project.assets.json -Raw | ConvertFrom-Json
    $runtime = Get-Content (Join-Path $publish 'GXDLMSDirector.runtimeconfig.json') -Raw | ConvertFrom-Json
    foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
        $package = "$($framework.name.ToLowerInvariant()).runtime.win-x64"
        $packageDirectory = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder "$package/$($framework.version)"
            if (Test-Path -LiteralPath $candidate) { $packageDirectory = $candidate; break }
        }
        if (-not $packageDirectory) { throw "Missing runtime package notices: $package" }
        $noticeDirectory = Join-Path $publish "licenses/runtime/$package/$($framework.version)"
        New-Item -ItemType Directory -Path $noticeDirectory -Force | Out-Null
        $notices = @(Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object {
            $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\..*)?$|\.versions\.txt$'
        })
        if (-not ($notices | Where-Object { $_.Name -like 'LICENSE*' })) { throw "Missing license: $package" }
        $notices | Copy-Item -Destination $noticeDirectory
    }
    $manifest = [ordered]@{
        configuration = 'Release'; runtimeIdentifier = 'win-x64'; selfContained = $true
        directorCommit = $directorCommit; uiCommit = $uiCommit; sdk = $sdk
        dashboards = (-not $DisableDashboards)
        command = 'dotnet publish Development/GXDLMSDirector46.csproj -c Release -p:PublishProfile=WindowsX64 -p:EnableDashboards=' + $enabled
        runtimeFrameworks = $runtime.runtimeOptions.includedFrameworks
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $publish 'BUILD-MANIFEST.json')
    foreach ($required in @('GXDLMSDirector.exe', 'Gurux.DLMS.dll', 'Gurux.DLMS.UI.dll',
            'coreclr.dll', 'System.Windows.Forms.dll', 'LICENSE', 'THIRD-PARTY-NOTICES', 'licenses/dependency-inventory.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publish $required))) { throw "Missing publish file: $required" }
    }
    $archive = Join-Path $output 'GXDLMSDirector-win-x64-Release.zip'
    $symbolArchive = Join-Path $output 'GXDLMSDirector-win-x64-symbols.zip'
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive
    Compress-Archive -Path (Join-Path $symbols '*') -DestinationPath $symbolArchive
    Get-FileHash -Algorithm SHA256 -LiteralPath $archive, $symbolArchive |
        ForEach-Object { '{0}  {1}' -f $_.Hash, (Split-Path $_.Path -Leaf) } |
        Set-Content (Join-Path $output 'SHA256SUMS.txt')
    Write-Output "Release candidate: $archive"
    Write-Output "Private symbols: $symbolArchive"
} finally { Pop-Location }
