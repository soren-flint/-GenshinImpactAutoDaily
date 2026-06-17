Add-Type -AssemblyName System.Drawing

$srcPath = 'D:\图片\游戏图\宵宫.jpg'
$outDir  = 'D:\MIHOYO\GenShinTool\每日驱动\自动化'
$icoPath = Join-Path $outDir 'app.ico'
$pngPath = Join-Path $outDir 'app.png'

if (-not (Test-Path -LiteralPath $srcPath)) {
    Write-Error ('Source not found: ' + $srcPath)
    exit 1
}

$src = [System.Drawing.Image]::FromFile($srcPath)
Write-Host ('Source size: ' + $src.Width + 'x' + $src.Height)

$side = [Math]::Min($src.Width, $src.Height)
$x0   = [int](($src.Width  - $side) / 2)
$y0   = [int](($src.Height - $side) / 2)
$cropRect = New-Object System.Drawing.Rectangle($x0, $y0, $side, $side)
$destRect = New-Object System.Drawing.Rectangle(0, 0, $side, $side)
$square = New-Object System.Drawing.Bitmap($side, $side)
$g = [System.Drawing.Graphics]::FromImage($square)
$g.DrawImage($src, $destRect, $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()

$png256 = New-Object System.Drawing.Bitmap(256, 256)
$g2 = [System.Drawing.Graphics]::FromImage($png256)
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g2.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g2.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g2.DrawImage($square, 0, 0, 256, 256)
$g2.Dispose()
$png256.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

$sizes = @(256, 128, 64, 48, 32, 16)
$pngBytesList = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $gg = [System.Drawing.Graphics]::FromImage($bmp)
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $gg.DrawImage($square, 0, 0, $s, $s)
    $gg.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytesList += ,($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$count = $sizes.Count

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$count)

$dirSize = 6 + 16 * $count
$offset  = $dirSize
for ($i = 0; $i -lt $count; $i++) {
    $s     = $sizes[$i]
    $bytes = $pngBytesList[$i]
    $dimByte = if ($s -eq 256) { [byte]0 } else { [byte]$s }
    $bw.Write([byte]$dimByte)
    $bw.Write([byte]$dimByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $bytes.Length
}
foreach ($bytes in $pngBytesList) {
    $bw.Write($bytes)
}
$bw.Flush()
$bw.Close()
$fs.Close()

$src.Dispose()
$square.Dispose()
$png256.Dispose()

Write-Host ('Done. ICO: ' + $icoPath)
Write-Host ('Done. PNG: ' + $pngPath)
