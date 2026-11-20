$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolRoot = Join-Path $projectRoot '.tools'
$sdkRoot = Join-Path $toolRoot 'dotnet'
$sdkExe = Join-Path $sdkRoot 'dotnet.exe'
$sdkVersion = '10.0.404'
$sdkHash = '330ddd2665b43cd2e428216a0d8ea1ac7bb19b5434fdc530e62010dc3b52823a620bf95489d628e1ed6f5122401793ee4887cca4a019122361a1231497f632ee'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
if (Test-Path -LiteralPath $sdkExe) {
    $actual = & $sdkExe --version
    if ($actual -ne $sdkVersion) { throw "项目SDK为 $actual，预期 $sdkVersion；保留已有目录，请核查。" }
    Write-Output "项目SDK $actual 已就绪。"
    exit 0
}
New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
$zip = Join-Path $toolRoot "dotnet-sdk-$sdkVersion-win-x64.zip"
if (!(Test-Path -LiteralPath $zip)) {
    Invoke-WebRequest -Uri "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdkVersion/dotnet-sdk-$sdkVersion-win-x64.zip" -OutFile $zip
}
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA512).Hash.ToLowerInvariant() -ne $sdkHash) { throw 'SDK SHA512核验失败；保留下载供诊断，未执行解压。' }
Expand-Archive -LiteralPath $zip -DestinationPath $sdkRoot
& $sdkExe --info
if ($LASTEXITCODE -ne 0) { throw 'SDK启动失败。' }
