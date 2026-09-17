# Aurora World Clock - per-user installer (no admin required).
#   pwsh -File install.ps1                 install + Start Menu shortcut
#   pwsh -File install.ps1 -Desktop        also create a desktop shortcut
#   pwsh -File install.ps1 -Startup        also launch at sign-in
#   pwsh -File install.ps1 -Run            install and start now
param(
  [switch]$Desktop,
  [switch]$Startup,
  [switch]$Run,
  [string]$Target = (Join-Path $env:LOCALAPPDATA 'Programs\AuroraClock')
)

$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$exe = 'AuroraClock.exe'

if (-not (Test-Path (Join-Path $src $exe))) {
  throw "$exe not found next to this script. Run build.ps1 first, or use the portable zip."
}

Write-Host "Installing to $Target" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $Target | Out-Null

Get-ChildItem -Path $src -File | Where-Object { $_.Name -ne 'install.ps1' } |
  ForEach-Object { Copy-Item $_.FullName (Join-Path $Target $_.Name) -Force }

$installedExe = Join-Path $Target $exe
$shell = New-Object -ComObject WScript.Shell

# Start Menu shortcut
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$lnk = Join-Path $startMenu 'Aurora World Clock.lnk'
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = $installedExe
$sc.WorkingDirectory = $Target
$sc.Description = 'Glassmorphic always-on-top world clock'
$sc.Save()
Write-Host "  shortcut: $lnk"

if ($Desktop) {
  $dlnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Aurora World Clock.lnk'
  $sc2 = $shell.CreateShortcut($dlnk)
  $sc2.TargetPath = $installedExe
  $sc2.WorkingDirectory = $Target
  $sc2.Save()
  Write-Host "  shortcut: $dlnk"
}

# Add/Remove Programs entry
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AuroraWorldClock'
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty $uninstallKey 'DisplayName'     'Aurora World Clock'
Set-ItemProperty $uninstallKey 'DisplayVersion'  '1.2.1'
Set-ItemProperty $uninstallKey 'Publisher'       'Aurora'
Set-ItemProperty $uninstallKey 'DisplayIcon'     $installedExe
Set-ItemProperty $uninstallKey 'InstallLocation' $Target
Set-ItemProperty $uninstallKey 'NoModify'        1 -Type DWord
Set-ItemProperty $uninstallKey 'NoRepair'        1 -Type DWord
$uninstallCmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$Target\uninstall.ps1`""
Set-ItemProperty $uninstallKey 'UninstallString' $uninstallCmd
Set-ItemProperty $uninstallKey 'QuietUninstallString' $uninstallCmd
Write-Host "  registered in Apps & features"

if ($Startup) {
  $run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
  Set-ItemProperty $run 'AuroraWorldClock' "`"$installedExe`""
  Write-Host "  autostart enabled"
}

if ($Run) {
  Start-Process -FilePath $installedExe
  Write-Host "  started"
}

Write-Host "Installed. Uninstall from Apps & features or run uninstall.ps1." -ForegroundColor Green



