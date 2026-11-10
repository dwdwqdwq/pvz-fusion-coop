param([Parameter(Mandatory=$true)][string]$GameDir,[string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir -Dotnet $Dotnet
$game=(Resolve-Path -LiteralPath $GameDir).Path
$dist=Join-Path $PSScriptRoot 'dist'
$stage=Join-Path $dist 'release-0.11.0'
$zip=Join-Path $dist 'PvZ-Fusion-Coop-0.11.0.zip'
if((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $zip)){throw 'Release output already exists; choose a clean checkout to rebuild it.'}
$plugins=Join-Path $stage 'BepInEx/plugins/PvZCoop'
$licenses=Join-Path $stage 'licenses'
New-Item -ItemType Directory -Path $plugins,$licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/bin/Release/net6.0/PvZCoop.dll') -Destination $plugins
Copy-Item -LiteralPath (Join-Path $game 'BepInEx/plugins/PvZCoop/Steamworks.NET.dll') -Destination $plugins
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs/Steamworks.NET-LICENSE.txt') -Destination $licenses
@'
PvZ Fusion Co-op 0.11.0 — MOD ONLY

Close the game. Extract this archive into an existing compatible Fusion 4.1
folder with BepInEx IL2CPP installed, merging the BepInEx folder.
Launch the game normally. Select LANGUAGE in the main menu for English or
Ukrainian; restart after changing it. Both players need the mod installed.
For remote play, both players can join the same Radmin VPN or ZeroTier One
network and connect using the host VPN IPv4 address and the same TCP port.
The full game, loader and saves are not included.

Закрийте гру. Розпакуйте архів у папку власного Fusion 4.1 з BepInEx IL2CPP,
об'єднавши папки BepInEx. Запустіть гру. У меню LANGUAGE виберіть українську
й перезапустіть гру. Для гри на відстані обидва приєднайтеся до однієї мережі
Radmin VPN або ZeroTier One та введіть VPN IPv4-адресу ведучого й той самий порт.
Повної гри, завантажувача та збережень в архіві немає.
'@ | Set-Content -LiteralPath (Join-Path $stage 'INSTALL.txt') -Encoding utf8
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$checksum=Get-FileHash -LiteralPath $zip -Algorithm SHA256
($checksum.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Write-Output $zip
