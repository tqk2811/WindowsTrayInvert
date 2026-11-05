# Draws Assets\app.ico in Windows 11 Fluent style.
# Usage: powershell -File make-icon.ps1 <out.ico> [preview.png]
param([string]$OutIco, [string]$OutPng)
Add-Type -AssemblyName System.Drawing

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function Fill-Grad($g, $path, $c1, $c2, [float]$angle) {
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush $path.GetBounds(), $c1, $c2, $angle
    $g.FillPath($br, $path); $br.Dispose()
}

function New-Frame([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $f = $s / 256.0
    $small = $s -le 24

    # Tile: Windows accent blue gradient, soft top highlight.
    $tile = New-RoundRect (12 * $f) (12 * $f) (232 * $f) (232 * $f) (52 * $f)
    Fill-Grad $g $tile (C 76 194 255) (C 0 95 184) 60
    $hl = New-RoundRect (12 * $f) (12 * $f) (232 * $f) (116 * $f) (52 * $f)
    Fill-Grad $g $hl (C 255 255 255 60) (C 255 255 255 0) 90

    # Frosted taskbar pill.
    $barY = 168 * $f; $barH = 52 * $f
    $bar = New-RoundRect (32 * $f) $barY (192 * $f) $barH (18 * $f)
    $shadow = New-RoundRect (32 * $f) ($barY + 4 * $f) (192 * $f) $barH (18 * $f)
    $sb = New-Object System.Drawing.SolidBrush (C 0 40 90 70); $g.FillPath($sb, $shadow); $sb.Dispose()
    Fill-Grad $g $bar (C 255 255 255 250) (C 225 236 248 250) 90

    # Tray icons on the bar: Fluent colors, all visible.
    $colors = @((C 0 120 212), (C 16 160 90), (C 247 99 12))
    $xs = if ($small) { @(76, 128, 180) } else { @(76, 128, 180) }
    $sz = if ($small) { 34 * $f } else { 28 * $f }
    for ($i = 0; $i -lt 3; $i++) {
        $cx = $xs[$i] * $f; $cy = $barY + $barH / 2
        $ic = New-RoundRect ($cx - $sz / 2) ($cy - $sz / 2) $sz $sz ($sz * 0.28)
        $b = New-Object System.Drawing.SolidBrush $colors[$i]; $g.FillPath($b, $ic); $b.Dispose()
    }

    # Overflow chevron flipped "out": icons come up from the hidden menu.
    $penW = if ($small) { [Math]::Max(2.2, 30 * $f) } else { 22 * $f }
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $penW
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $top = if ($small) { 56 } else { 50 }
    $g.DrawLines($pen, [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF (78 * $f), (($top + 50) * $f)),
        (New-Object System.Drawing.PointF (128 * $f), ($top * $f)),
        (New-Object System.Drawing.PointF (178 * $f), (($top + 50) * $f))))
    if (-not $small) {
        $pen2 = New-Object System.Drawing.Pen (C 255 255 255 150), (16 * $f)
        $pen2.StartCap = 'Round'; $pen2.EndCap = 'Round'; $pen2.LineJoin = 'Round'
        $g.DrawLines($pen2, [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF (98 * $f), (136 * $f)),
            (New-Object System.Drawing.PointF (128 * $f), (106 * $f)),
            (New-Object System.Drawing.PointF (158 * $f), (136 * $f))))
        $pen2.Dispose()
    }
    $pen.Dispose()
    $g.Dispose()
    return $bmp
}

# ICO frame as 32bpp DIB (BITMAPINFOHEADER + bottom-up BGRA + AND mask): readable by every decoder.
function Get-DibBytes($bmp) {
    $s = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $maskStride = [int][Math]::Ceiling($s / 32.0) * 4
    $w.Write([UInt32]40); $w.Write([Int32]$s); $w.Write([Int32]($s * 2)); $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]0); $w.Write([UInt32]($s * $s * 4 + $maskStride * $s)); $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
        }
    }
    $w.Write((New-Object byte[] ($maskStride * $s))) # alpha channel is used; mask all zero
    $w.Flush()
    return , $ms.ToArray()
}

# 256px stays PNG (standard for the large frame, keeps the file small).
$sizes = 16, 20, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $b = New-Frame $s
    if ($s -eq 256) {
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        if ($OutPng) { $b.Save($OutPng, [System.Drawing.Imaging.ImageFormat]::Png) }
        $bytes = $ms.ToArray()
    } else {
        $bytes = Get-DibBytes $b
    }
    $b.Dispose()
    , $bytes
}

$fs = [System.IO.File]::Create($OutIco)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $pngs[$i]
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$d.Length); $bw.Write([UInt32]$offset)
    $offset += $d.Length
}
foreach ($d in $pngs) { $bw.Write($d) }
$bw.Dispose()
