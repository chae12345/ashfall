$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework C# compiler is required.' }
$outputDirectory = Join-Path $PSScriptRoot 'CoreChecks\bin'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$outputFile = Join-Path $outputDirectory 'CoreChecks.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets\Scripts\Core') -Filter '*.cs' | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot 'CoreChecks\Program.cs'
$sources += Join-Path $projectRoot 'Assets\Editor\AshfallCoreChecks.cs'
& $compiler /nologo /warnaserror+ "/out:$outputFile" $sources
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
& $outputFile
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
