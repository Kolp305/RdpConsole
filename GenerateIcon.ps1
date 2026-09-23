# Генерує RdpConsole.ico (16/32/48/256) без зовнішніх залежностей -- System.Drawing.
# Малює просту емблему "монітор + індикатор з'єднання" на синьому заокругленому тлі.

Add-Type -AssemblyName System.Drawing

function New-IconFrame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $accent     = [System.Drawing.Color]::FromArgb(255, 0, 103, 184)   # синій, у стилі RDP
    $accentDark = [System.Drawing.Color]::FromArgb(255, 0, 58, 112)
    $online     = [System.Drawing.Color]::FromArgb(255, 43, 193, 108)  # зелений індикатор

    # --- заокруглене синє тло ---
    $pad = [Math]::Max(1.0, $size * 0.035)
    $rect = New-Object System.Drawing.RectangleF $pad, $pad, ($size - 2*$pad), ($size - 2*$pad)
    $radius = $size * 0.22
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right-$d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right-$d, $rect.Bottom-$d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom-$d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brushBg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF $rect.X, $rect.Y),
        (New-Object System.Drawing.PointF $rect.Right, $rect.Bottom),
        $accent, $accentDark)
    $g.FillPath($brushBg, $path)

    # --- монітор (біла рамка екрана) ---
    $screenW = $size * 0.56
    $screenH = $size * 0.40
    $screenX = ($size - $screenW) / 2
    $screenY = $size * 0.18
    $screenRadius = $size * 0.05

    $sp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sd = $screenRadius * 2
    $sp.AddArc($screenX, $screenY, $sd, $sd, 180, 90)
    $sp.AddArc($screenX+$screenW-$sd, $screenY, $sd, $sd, 270, 90)
    $sp.AddArc($screenX+$screenW-$sd, $screenY+$screenH-$sd, $sd, $sd, 0, 90)
    $sp.AddArc($screenX, $screenY+$screenH-$sd, $sd, $sd, 90, 90)
    $sp.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::White, $sp)

    # --- внутрішня "поверхня" екрана ---
    $m = $size * 0.032
    $innerRect = New-Object System.Drawing.RectangleF ($screenX+$m), ($screenY+$m), ($screenW-2*$m), ($screenH-2*$m)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $accentDark), $innerRect)

    # --- підставка монітора ---
    $standW = $size * 0.09
    $standH = $size * 0.075
    $standX = ($size - $standW) / 2
    $standY = $screenY + $screenH - ($size*0.01)
    $g.FillRectangle([System.Drawing.Brushes]::White, $standX, $standY, $standW, $standH)
    $baseW = $size * 0.30
    $baseH = $size * 0.045
    $baseRect = New-Object System.Drawing.RectangleF (($size-$baseW)/2), ($standY+$standH-$size*0.01), $baseW, $baseH
    $g.FillRectangle([System.Drawing.Brushes]::White, $baseRect)

    # --- зелений індикатор з'єднання (кружечок з білою облямівкою) знизу праворуч екрана ---
    $dotD = $size * 0.24
    $dotX = $screenX + $screenW - $dotD*0.62
    $dotY = $screenY + $screenH - $dotD*0.62
    $ringPad = $size * 0.028
    $g.FillEllipse([System.Drawing.Brushes]::White, ($dotX-$ringPad), ($dotY-$ringPad), ($dotD+2*$ringPad), ($dotD+2*$ringPad))
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $online), $dotX, $dotY, $dotD, $dotD)

    $g.Dispose()
    return $bmp
}

function Get-DibFrameBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $rowBytes = $w * 4
        $topDown = New-Object byte[] ($rowBytes * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $topDown, 0, $topDown.Length)
    } finally {
        $bmp.UnlockBits($data)
    }

    # XOR-маска: пікселі BGRA, рядки знизу вгору (як у звичайному BMP)
    $xor = New-Object byte[] ($rowBytes * $h)
    for ($row = 0; $row -lt $h; $row++) {
        [Array]::Copy($topDown, $row * $rowBytes, $xor, ($h - 1 - $row) * $rowBytes, $rowBytes)
    }

    # AND-маска: 1 біт/піксель, рядки вирівняні до 4 байт, усі нулі (прозорість -- лише через альфа-канал)
    $maskRowBytes = [Math]::Ceiling($w / 32.0) * 4
    $and = New-Object byte[] ($maskRowBytes * $h)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([UInt32]40)        # biSize
    $bw.Write([Int32]$w)         # biWidth
    $bw.Write([Int32]($h * 2))   # biHeight (XOR + AND)
    $bw.Write([UInt16]1)         # biPlanes
    $bw.Write([UInt16]32)        # biBitCount
    $bw.Write([UInt32]0)         # biCompression = BI_RGB
    $bw.Write([UInt32]($xor.Length + $and.Length)) # biSizeImage
    $bw.Write([Int32]0)          # biXPelsPerMeter
    $bw.Write([Int32]0)          # biYPelsPerMeter
    $bw.Write([UInt32]0)         # biClrUsed
    $bw.Write([UInt32]0)         # biClrImportant
    $bw.Write($xor)
    $bw.Write($and)
    $bw.Flush()
    # кома запобігає "розгортанню" масиву байтів у Object[] під час повернення з функції
    return ,$ms.ToArray()
}

$sizes = @(16, 24, 32, 48, 64, 128)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- прев'ю у PNG (256px) для візуальної перевірки ---
$preview = New-IconFrame 256
$preview.Save((Join-Path $scriptDir "icon_preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

# --- збірка .ico вручну (класичний формат: DIB-кадри, сумісні зі старим компілятором ресурсів csc.exe) ---
$images = foreach ($s in $sizes) {
    $bmp = New-IconFrame $s
    [byte[]]$frameBytes = Get-DibFrameBytes $bmp
    [PSCustomObject]@{ Size = $s; Bytes = $frameBytes }
    $bmp.Dispose()
}

$icoPath = Join-Path $scriptDir "RdpConsole.ico"
$fs = New-Object System.IO.FileStream $icoPath, ([System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs

# ICONDIR
$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type = icon
$bw.Write([UInt16]$images.Count)

$headerSize = 6 + (16 * $images.Count)
$offset = $headerSize

foreach ($img in $images) {
    $bw.Write([byte]$img.Size)   # width
    $bw.Write([byte]$img.Size)   # height
    $bw.Write([byte]0)           # color palette
    $bw.Write([byte]0)           # reserved
    $bw.Write([UInt16]1)         # color planes
    $bw.Write([UInt16]32)        # bits per pixel
    $bw.Write([UInt32]$img.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $img.Bytes.Length
}

foreach ($img in $images) {
    $bw.Write($img.Bytes)
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "OK: $icoPath (preview: $scriptDir\icon_preview.png)" -ForegroundColor Green
