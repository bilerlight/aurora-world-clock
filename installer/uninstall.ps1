# Aurora World Clock - uninstaller (removes the per-user install).
param([string]$Target = (Join-Path $env:LOCALAPPDATA 'Programs\AuroraClock'))

$ErrorActionPreference = 'SilentlyContinue'

Get-Process AuroraClock | Stop-Process -Force

$lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Aurora World Clock.lnk'
if (Test-Path $lnk) { Remove-Item $lnk -Force }

$dlnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Aurora World Clock.lnk'
if (Test-Path $dlnk) { Remove-Item $dlnk -Force }

Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AuroraWorldClock' -Recurse -Force
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' 'AuroraWorldClock'

Start-Sleep -Milliseconds 300
Remove-Item $Target -Recurse -Force

Write-Output "Aurora World Clock removed. (Settings kept in %AppData%\AuroraClock)"
