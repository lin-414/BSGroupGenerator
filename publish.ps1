# Publish BS Group Generator as a single-file exe.
# Default: framework-dependent (small; requires .NET 8 Desktop Runtime on user machine).
# Use -SelfContained to bundle the runtime (about 70MB+, no prerequisites).
param(
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "dist"

$dotnetArgs = @(
    "publish", (Join-Path $root "src\BSGroupGenerator\BSGroupGenerator.csproj"),
    "-c", "Release",
    "-r", "win-x64",
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:DebugType=none",
    "/p:AllowedReferenceRelatedFileExtensions=none",
    "-o", $out
)
if ($SelfContained) {
    $dotnetArgs += "--self-contained", "true"
} else {
    $dotnetArgs += "--self-contained", "false"
}

dotnet @dotnetArgs
Write-Host ""
Write-Host "Output: $out" -ForegroundColor Green
Get-ChildItem $out | Format-Table Name, Length
