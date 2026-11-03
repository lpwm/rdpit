# Generates a multi-resolution .ico for RdpIt: a blue rounded tile with a white
# monitor glyph and a small green "connected" dot. Pure GDI+, no external tools.
# Output: assets\rdpit.ico
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$assets = Join-Path $PSScriptRoot "assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$icoPath = Join-Path $assets "rdpit.ico"

function New-GlyphBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [double]$size
    # Rounded background tile with a vertical blue gradient.
    $pad = [Math]::Max(1, [int]($s * 0.06))
    $rect = New-Object System.Drawing.Rectangle($pad, $pad, ($size - 2*$pad), ($size - 2*$pad))
    $radius = [int]($s * 0.18)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    if ($d -lt 2) { $d = 2 }
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc(($rect.Right - $d), $rect.Y, $d, $d, 270, 90)
    $path.AddArc(($rect.Right - $d), ($rect.Bottom - $d), $d, $d, 0, 90)
    $path.AddArc($rect.X, ($rect.Bottom - $d), $d, $d, 90, 90)
    $path.CloseFigure()

    $c1 = [System.Drawing.Color]::FromArgb(255, 33, 118, 211)   # #2176D3
    $c2 = [System.Drawing.Color]::FromArgb(255, 18, 78, 150)    # darker blue
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 90)
    $g.FillPath($brush, $path)

    # White monitor body.
    $mw = $s * 0.52
    $mh = $s * 0.38
    $mx = ($s - $mw) / 2
    $my = $s * 0.26
    $monitor = New-Object System.Drawing.RectangleF($mx, $my, $mw, $mh)
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $screenRadius = [single]([Math]::Max(1.0, $s * 0.04))
    $g.FillRectangle($white, $monitor)

    # Inner screen (blue) inset.
    $inset = [single]([Math]::Max(1.0, $s * 0.05))
    if (($monitor.Width - 2*$inset) -gt 0 -and ($monitor.Height - 2*$inset) -gt 0) {
        $inner = New-Object System.Drawing.RectangleF(
            ($monitor.X + $inset), ($monitor.Y + $inset),
            ($monitor.Width - 2*$inset), ($monitor.Height - 2*$inset))
        $innerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 20, 90, 170))
        $g.FillRectangle($innerBrush, $inner)
        $innerBrush.Dispose()
    }

    # Monitor stand.
    $standW = $s * 0.16
    $standH = $s * 0.06
    $standX = ($s - $standW) / 2
    $standY = $monitor.Bottom + $s * 0.02
    $g.FillRectangle($white, (New-Object System.Drawing.RectangleF($standX, $standY, $standW, $standH)))
    $baseW = $s * 0.30
    $baseH = [Math]::Max(1.0, $s * 0.04)
    $baseX = ($s - $baseW) / 2
    $baseY = $standY + $standH
    $g.FillRectangle($white, (New-Object System.Drawing.RectangleF($baseX, $baseY, $baseW, $baseH)))

    # Green "connected" dot, lower-right of the screen (skip on tiny icons).
    if ($size -ge 32) {
        $dotD = [single]($s * 0.14)
        $dotX = [single]($monitor.Right - $dotD * 0.9)
        $dotY = [single]($monitor.Top - $dotD * 0.25)
        $green = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 46, 204, 113))
        $g.FillEllipse($green, $dotX, $dotY, $dotD, $dotD)
        $green.Dispose()
    }

    $brush.Dispose(); $white.Dispose(); $path.Dispose(); $g.Dispose()
    return $bmp
}

# Build the .ico container manually (ICONDIR + entries + PNG-encoded images).
$bitmaps = @{}
foreach ($sz in $sizes) { $bitmaps[$sz] = New-GlyphBitmap $sz }

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICONDIR
$bw.Write([UInt16]0)            # reserved
$bw.Write([UInt16]1)            # type = icon
$bw.Write([UInt16]$sizes.Count) # image count

# Encode each image to PNG, collect bytes.
$pngs = @()
foreach ($sz in $sizes) {
    $imgMs = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($imgMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,($imgMs.ToArray())
    $imgMs.Dispose()
}

# Directory entries. Image data starts after ICONDIR(6) + entries(16 each).
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $bytes = $pngs[$i]
    $wb = if ($sz -ge 256) { 0 } else { $sz }   # 0 means 256 in ICO
    $bw.Write([Byte]$wb)        # width
    $bw.Write([Byte]$wb)        # height
    $bw.Write([Byte]0)          # colors in palette
    $bw.Write([Byte]0)          # reserved
    $bw.Write([UInt16]1)        # color planes
    $bw.Write([UInt16]32)       # bits per pixel
    $bw.Write([UInt32]$bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($bytes in $pngs) { $bw.Write($bytes) }

$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$bw.Dispose(); $ms.Dispose()
foreach ($sz in $sizes) { $bitmaps[$sz].Dispose() }

Write-Host "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes, sizes: $($sizes -join ', '))" -ForegroundColor Green