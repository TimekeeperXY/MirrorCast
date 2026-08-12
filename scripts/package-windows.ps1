param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\MirrorCast\MirrorCast.csproj"
$dist = Join-Path $root "dist"
$publish = Join-Path $dist "MirrorCast-windows-x64"
$archive = Join-Path $dist "MirrorCast-windows-x64.zip"

if (Test-Path -LiteralPath $publish) {
    Remove-Item -LiteralPath $publish -Recurse -Force
}
New-Item -ItemType Directory -Path $publish -Force | Out-Null

dotnet publish $project -c $Configuration -r $Runtime --self-contained `
    -p:PublishSingleFile=true -o $publish

$required = @(
    "MirrorCast.exe",
    "Resources\android-tools\windows-x86_64\platform-tools\adb.exe",
    "Resources\android-tools\windows-x86_64\scrcpy\scrcpy.exe",
    "Resources\android-tools\windows-x86_64\scrcpy\scrcpy-server",
    "Resources\android-tools\windows-x86_64\scrcpy\SDL3.dll"
)
foreach ($relative in $required) {
    $path = Join-Path $publish $relative
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing packaged Android tool: $relative"
    }
}

if (Test-Path -LiteralPath $archive) {
    Remove-Item -LiteralPath $archive -Force
}
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $archive -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $archive -Algorithm SHA256
Set-Content -LiteralPath "$archive.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $archive)" -Encoding ascii

Write-Host "Packaged: $archive"
Write-Host "SHA-256: $($hash.Hash.ToLowerInvariant())"
