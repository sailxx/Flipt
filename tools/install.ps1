$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$dst = Join-Path $env:LOCALAPPDATA 'Flipt'

# logo.png -> flipt.ico (PNG-кадры, углы вне плитки прозрачные)
$src = New-Object System.Drawing.Bitmap (Join-Path $root 'logo.png')
$w = $src.Width; $h = $src.Height
$base = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($base); $g.DrawImage($src, 0, 0, $w, $h); $g.Dispose(); $src.Dispose()
function IsBg($c) { $c.R -gt 200 -and $c.G -gt 200 -and $c.B -gt 200 }
$seen = New-Object 'bool[,]' $w, $h
$q = New-Object System.Collections.Generic.Queue[object]
foreach ($p in @(@(0,0), @(($w-1),0), @(0,($h-1)), @(($w-1),($h-1)))) { $q.Enqueue($p); $seen[$p[0],$p[1]] = $true }
while ($q.Count) {
  $p = $q.Dequeue(); $x = $p[0]; $y = $p[1]
  if (-not (IsBg $base.GetPixel($x, $y))) { continue }
  $base.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
  foreach ($d in @(@(1,0), @(-1,0), @(0,1), @(0,-1))) {
    $nx = $x + $d[0]; $ny = $y + $d[1]
    if ($nx -ge 0 -and $ny -ge 0 -and $nx -lt $w -and $ny -lt $h -and -not $seen[$nx,$ny]) { $seen[$nx,$ny] = $true; $q.Enqueue(@($nx, $ny)) }
  }
}
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
  $b = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gg = [System.Drawing.Graphics]::FromImage($b)
  $gg.InterpolationMode = 'HighQualityBicubic'; $gg.SmoothingMode = 'HighQuality'; $gg.PixelOffsetMode = 'HighQuality'
  $gg.DrawImage($base, 0, 0, $s, $s); $gg.Dispose()
  $ms = New-Object System.IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
  , $ms.ToArray()
}
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$off = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $dim = if ($s -eq 256) { 0 } else { $s }
  $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$off)
  $off += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $root 'flipt.ico'), $out.ToArray())
$base.Dispose()

# сборка и установка
Get-Process Flipt -ErrorAction SilentlyContinue | Stop-Process -Force
& (Join-Path $root 'build.cmd')
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item (Join-Path $root 'Flipt.exe') $dst -Force
Copy-Item (Join-Path $root 'flipt.ico') $dst -Force

# ярлыки: рабочий стол, Пуск, панель задач
$sh = New-Object -ComObject WScript.Shell
$taskbar = Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'
Remove-Item (Join-Path $taskbar 'Flipt (2).lnk') -ErrorAction SilentlyContinue
$paths = @(
  (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Flipt.lnk'),
  (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Flipt.lnk'),
  (Join-Path $taskbar 'Flipt.lnk')
)
foreach ($p in $paths) {
  $l = $sh.CreateShortcut($p)
  $l.TargetPath = Join-Path $dst 'Flipt.exe'
  $l.WorkingDirectory = $dst
  $l.IconLocation = (Join-Path $dst 'flipt.ico') + ',0'
  $l.Save()
  "ok $p"
}

# сброс кэша значков и перезапуск проводника
ie4uinit.exe -ClearIconCache
Stop-Process -Name explorer -Force
for ($i = 0; $i -lt 10 -and -not (Get-Process explorer -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
Start-Sleep 3
if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
'done'
