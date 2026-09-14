# Publish BS Group Generator (WPF).
# Default: multi-file framework-dependent folder into dist-wpf\ (零件摊开，
#   requires .NET 10 Desktop Runtime); add -SelfContained to bundle the
#   runtime (no prerequisites), and/or -SingleFile to bundle everything
#   back into one exe (compressed, ~61MB).
param(
    [switch]$SelfContained,
    [switch]$SingleFile
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "dist-wpf"
$project = Join-Path $root "src\BSGroupGenerator.Wpf\BSGroupGenerator.Wpf.csproj"

$dotnetArgs = @(
    "publish", $project,
    "-c", "Release",
    "-r", "win-x64",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:DebugType=none",
    "/p:AllowedReferenceRelatedFileExtensions=none"
)
if ($SingleFile) {
    $dotnetArgs += "/p:PublishSingleFile=true"
}
if ($SelfContained) {
    $dotnetArgs += "--self-contained", "true"
} else {
    $dotnetArgs += "--self-contained", "false"
}
$dotnetArgs += "-o", $out

dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    # dotnet 是原生命令，失败不会触发 ErrorActionPreference；这里必须显式拦截，
    # 否则下面的文件列表会把旧产物误当成发布成功
    throw "dotnet publish 失败（exit $LASTEXITCODE）"
}
Write-Host ""
Write-Host "Output: $out" -ForegroundColor Green
Get-ChildItem $out | Format-Table Name, Length
