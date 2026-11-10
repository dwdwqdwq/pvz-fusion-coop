param([Parameter(Mandatory=$true)][string]$GameDir,[string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$resolved=(Resolve-Path -LiteralPath $GameDir).Path
& $Dotnet build (Join-Path $PSScriptRoot 'src/PvZCoop.csproj') -c Release "-p:GameDir=$resolved" --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Write-Output 'Built src/bin/Release/net6.0/PvZCoop.dll. This script does not install or replace files in the game.'
