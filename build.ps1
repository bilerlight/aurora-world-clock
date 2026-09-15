# Builds Aurora World Clock.
#   pwsh -File build.ps1                 -> self-contained portable build + zip (+ setup.exe if Inno Setup exists)
#   pwsh -File build.ps1 -SkipInstaller  -> portable only
param(
  [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$pub  = Join-Path $dist 'app'

Write-Host "==> Generating icon" -ForegroundColor Cyan
& pwsh -NoProfile -File (Join-Path $root 'tools\make-icon.ps1')

Write-Host "==> Publishing self-contained win-x64" -ForegroundColor Cyan
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $pub | Out-Null

dotnet publish (Join-Path $root 'AuroraClock.csproj') `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=none `
  -p:DebugSymbols=false `
  -o $pub

Copy-Item (Join-Path $root 'installer\install.ps1')   $pub -Force
Copy-Item (Join-Path $root 'installer\uninstall.ps1') $pub -Force
if (Test-Path (Join-Path $root 'README.md')) { Copy-Item (Join-Path $root 'README.md') $pub -Force }

$zip = Join-Path $dist 'AuroraClock-portable-win-x64.zip'
Write-Host "==> Packing portable zip" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $pub '*') -DestinationPath $zip -Force

if (-not $SkipInstaller) {
  $candidates = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
  )
  $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
  if (-not $iscc) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
  }

  if ($iscc) {
    Write-Host "==> Building installer with $iscc" -ForegroundColor Cyan
    & $iscc "/DAppDir=$pub" "/O$dist" (Join-Path $root 'installer\setup.iss')
  } else {
    Write-Host "==> Inno Setup not found: skipping setup.exe." -ForegroundColor Yellow
    Write-Host "    Install it (https://jrsoftware.org/isdl.php) and re-run, or use installer\install.ps1." -ForegroundColor Yellow
  }
}

Write-Host ""
Write-Host "Done. Output:" -ForegroundColor Green
Get-ChildItem $dist | Select-Object Name, @{ n = 'Size'; e = { if ($_.PSIsContainer) { '<dir>' } else { '{0:N1} MB' -f ($_.Length / 1MB) } } } | Format-Table -AutoSize
