param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdkExe = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $sdkExe)) { throw '请先运行 scripts\bootstrap.ps1 准备项目内 .NET SDK。' }
$env:DOTNET_ROOT = Split-Path -Parent $sdkExe
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
Push-Location $projectRoot
try {
    & $sdkExe build 'src\GitScope.App\GitScope.App.csproj' -c Release --configfile 'NuGet.Config'
    if ($LASTEXITCODE -ne 0) { throw '应用构建失败。' }
    if ($Publish) {
        & $sdkExe publish 'src\GitScope.App\GitScope.App.csproj' -c Release --no-restore --self-contained false -o 'artifacts\app'
        if ($LASTEXITCODE -ne 0) { throw '发布失败。' }
    }
}
finally { Pop-Location }
