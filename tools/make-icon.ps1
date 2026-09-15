# Generates Assets/app.ico (multi-size, PNG-compressed) so the exe/shortcuts get a proper icon.
# Run:  pwsh -File tools/make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-ClockBitmap([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::Transparent)

  $pad  = [float]($size * 0.055)
  $rect = New-Object System.Drawing.RectangleF($pad, $pad, [float]($size - 2 * $pad), [float]($size - 2 * $pad))
  $bg   = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            [System.Drawing.Color]::FromArgb(255, 96, 158, 240),
            [System.Drawing.Color]::FromArgb(255, 34, 60, 116),
            45)
  $g.FillEllipse($bg, $rect)

  [float]$cx = $size / 2.0
  [float]$cy = $size / 2.0
  [float]$r  = ($size - 2 * $pad) / 2.0

  # outer ring
  $ringPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(215, 255, 255, 255), [float][Math]::Max(1.0, $size * 0.045))
  [float]$rr = $r * 0.72
  $g.DrawEllipse($ringPen, $cx - $rr, $cy - $rr, $rr * 2, $rr * 2)

  # hour ticks
  $tickPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 255, 255, 255), [float][Math]::Max(0.8, $size * 0.028))
  for ($i = 0; $i -lt 12; $i++) {
    $a = $i * 30.0 * [Math]::PI / 180.0
    [float]$sx = $cx + [Math]::Sin($a) * ($r * 0.60)
    [float]$sy = $cy - [Math]::Cos($a) * ($r * 0.60)
    [float]$ex = $cx + [Math]::Sin($a) * ($r * 0.50)
    [float]$ey = $cy - [Math]::Cos($a) * ($r * 0.50)
    $g.DrawLine($tickPen, $sx, $sy, $ex, $ey)
  }

  # hands (10:10-ish)
  $handPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [float][Math]::Max(1.5, $size * 0.072))
  $handPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $handPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($handPen, $cx, $cy, $cx - $r * 0.36, $cy - $r * 0.34)
  $g.DrawLine($handPen, $cx, $cy, $cx + $r * 0.40, $cy - $r * 0.46)

  # accent second hand
  $secPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 124, 196, 255), [float][Math]::Max(1.0, $size * 0.038))
  $secPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $secPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
  $g.DrawLine($secPen, $cx, $cy, $cx, $cy + $r * 0.66)

  # hub
  $hub = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
  $g.FillEllipse($hub, $cx - $size * 0.048, $cy - $size * 0.048, $size * 0.096, $size * 0.096)

  $g.Dispose(); $bg.Dispose(); $ringPen.Dispose(); $tickPen.Dispose(); $handPen.Dispose(); $secPen.Dispose(); $hub.Dispose()
  return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($s in $sizes) {
  $bmp = New-ClockBitmap $s
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $images += , @($s, $ms.ToArray())
  $ms.Dispose(); $bmp.Dispose()
}

$out = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Assets\app.ico'))
New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null

$count  = $images.Count
$offset = 6 + $count * 16

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$count)
foreach ($img in $images) {
  $s = [int]$img[0]; $data = $img[1]
  $dim = [byte]$(if ($s -ge 256) { 0 } else { $s })
  $bw.Write($dim); $bw.Write($dim)
  $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32)
  $bw.Write([UInt32]$data.Length)
  $bw.Write([UInt32]$offset)
  $offset += $data.Length
}
foreach ($img in $images) { $bw.Write($img[1]) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Output ("wrote {0} ({1} bytes, {2} sizes)" -f $out, (Get-Item $out).Length, $count)
