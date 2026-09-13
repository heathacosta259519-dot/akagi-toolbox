param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\ContextMenuEditor\ContextMenuEditor.csproj"
$tests = Join-Path $root "tests\ContextMenuEditor.Tests\ContextMenuEditor.Tests.csproj"
$dist = Join-Path $root "dist"

if (-not $SkipTests) {
    Write-Host "==> 运行测试" -ForegroundColor Cyan
    dotnet test $tests -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "测试未通过，已中止发布"
    }
}

Write-Host "==> 发布自包含单文件 exe" -ForegroundColor Cyan
if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}

dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $dist `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "发布失败"
}

$exe = Join-Path $dist "ContextMenuEditor.exe"
$version = (Get-Item $exe).VersionInfo.ProductVersion.Split('+')[0]
$zip = Join-Path $dist "context-menu-editor-v$version-$Runtime.zip"
Compress-Archive -Path $exe -DestinationPath $zip -Force

$exeSize = [math]::Round((Get-Item $exe).Length / 1MB, 1)
$zipSize = [math]::Round((Get-Item $zip).Length / 1MB, 1)

Write-Host "==> 完成" -ForegroundColor Green
Write-Host "exe : $exe ($exeSize MB)"
Write-Host "zip : $zip ($zipSize MB)"
