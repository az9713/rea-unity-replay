$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'env.ps1')
Set-Location -LiteralPath $ProjectRoot
foreach ($Project in @('tests/CoreTests.csproj','tests/ApiContract.csproj','cli/UnityReplay.Cli.csproj')) {
    & $Dotnet restore $Project --configfile (Join-Path $ProjectRoot 'NuGet.Config')
    if ($LASTEXITCODE) { throw "Restore failed: $Project" }
}
& $Dotnet run --project tests/CoreTests.csproj --no-restore -- $ProjectRoot
if ($LASTEXITCODE) { throw 'Real core tests failed' }
& $Dotnet build tests/ApiContract.csproj --no-restore -c Release
if ($LASTEXITCODE) { throw 'Compile against handwritten API shims failed' }
& $Dotnet run --project cli/UnityReplay.Cli.csproj --no-restore -- demo artifacts/demo
if ($LASTEXITCODE) { throw 'Standalone C# demo failed' }
python (Join-Path $PSScriptRoot 'record_evidence.py')
if ($LASTEXITCODE) { throw 'Evidence recording failed' }
