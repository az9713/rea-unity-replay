$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'env.ps1')
Set-Location -LiteralPath $ProjectRoot
& $Dotnet restore cli/UnityReplay.Cli.csproj --runtime win-x64 --configfile (Join-Path $ProjectRoot 'NuGet.Config')
if ($LASTEXITCODE) { throw 'Self-contained restore failed' }
& $Dotnet publish cli/UnityReplay.Cli.csproj --no-restore --runtime win-x64 --self-contained true -c Release -o .runtime/publish -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE) { throw 'Self-contained publication failed' }
$RuntimeVersion = '9.0.22'
$RuntimePackage = Join-Path $env:NUGET_PACKAGES "microsoft.netcore.app.runtime.win-x64/$RuntimeVersion"
foreach ($Name in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')) {
    Copy-Item -LiteralPath (Join-Path $RuntimePackage $Name) -Destination (Join-Path $ProjectRoot ".runtime/publish/$Name")
}
& (Join-Path $ProjectRoot '.runtime/publish/UnityReplay.Cli.exe') demo artifacts/published-demo
if ($LASTEXITCODE) { throw 'Published EXE demo failed' }
& (Join-Path $ProjectRoot '.runtime/publish/UnityReplay.Cli.exe') verify artifacts/published-demo/manifest.json artifacts/published-demo/baseline.json artifacts/published-demo/context.json artifacts/published-demo artifacts/published-demo/verified-plan.json
if ($LASTEXITCODE) { throw 'Published EXE file-verified replay failed' }
python (Join-Path $PSScriptRoot 'record_publish.py')
if ($LASTEXITCODE) { throw 'Publish manifest recording failed' }
