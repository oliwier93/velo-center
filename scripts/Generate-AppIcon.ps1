[CmdletBinding()]
param(
    [string]$OutputPath = ".\\src\\VeloCenter.App\\Assets\\avalonia-logo.ico"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2

    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Color {
    param([string]$Hex)

    return [System.Drawing.ColorTranslator]::FromHtml($Hex)
}

function Draw-VeloCenterIcon {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $outerRect = [System.Drawing.RectangleF]::new($Size * 0.035, $Size * 0.035, $Size * 0.93, $Size * 0.93)
    $outerPath = New-RoundedRectanglePath -Rect $outerRect -Radius ($Size * 0.21)

    $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new(0, 0),
        [System.Drawing.PointF]::new($Size, $Size),
        (New-Color "#16051F"),
        (New-Color "#09040E"))

    $backgroundBlend = New-Object System.Drawing.Drawing2D.ColorBlend
    $backgroundBlend.Positions = @(0.0, 0.42, 0.72, 1.0)
    $backgroundBlend.Colors = @(
        (New-Color "#1B0C2E"),
        (New-Color "#120717"),
        (New-Color "#0D0614"),
        (New-Color "#06030A"))
    $backgroundBrush.InterpolationColors = $backgroundBlend
    $graphics.FillPath($backgroundBrush, $outerPath)

    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(110, (New-Color "#8A61AF")), [Math]::Max(2, $Size * 0.02))
    $graphics.DrawPath($borderPen, $outerPath)

    $glowBrushA = New-Object System.Drawing.Drawing2D.PathGradientBrush((New-RoundedRectanglePath -Rect ([System.Drawing.RectangleF]::new($Size * 0.08, $Size * 0.08, $Size * 0.44, $Size * 0.44)) -Radius ($Size * 0.16)))
    $glowBrushA.CenterColor = [System.Drawing.Color]::FromArgb(180, (New-Color "#4CEB6E"))
    $glowBrushA.SurroundColors = @([System.Drawing.Color]::FromArgb(0, (New-Color "#4CEB6E")))
    $graphics.FillEllipse($glowBrushA, $Size * 0.03, $Size * 0.03, $Size * 0.48, $Size * 0.48)

    $glowBrushB = New-Object System.Drawing.Drawing2D.PathGradientBrush((New-RoundedRectanglePath -Rect ([System.Drawing.RectangleF]::new($Size * 0.52, $Size * 0.52, $Size * 0.34, $Size * 0.34)) -Radius ($Size * 0.12)))
    $glowBrushB.CenterColor = [System.Drawing.Color]::FromArgb(168, (New-Color "#A669FF"))
    $glowBrushB.SurroundColors = @([System.Drawing.Color]::FromArgb(0, (New-Color "#A669FF")))
    $graphics.FillEllipse($glowBrushB, $Size * 0.50, $Size * 0.52, $Size * 0.38, $Size * 0.38)

    $plateRect = [System.Drawing.RectangleF]::new($Size * 0.17, $Size * 0.18, $Size * 0.66, $Size * 0.64)
    $platePath = New-RoundedRectanglePath -Rect $plateRect -Radius ($Size * 0.16)
    $plateBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new($plateRect.Left, $plateRect.Top),
        [System.Drawing.PointF]::new($plateRect.Right, $plateRect.Bottom),
        [System.Drawing.Color]::FromArgb(58, (New-Color "#25183C")),
        [System.Drawing.Color]::FromArgb(92, (New-Color "#16101F")))
    $graphics.FillPath($plateBrush, $platePath)

    $platePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(96, (New-Color "#FF95D9")), [Math]::Max(2, $Size * 0.012))
    $graphics.DrawPath($platePen, $platePath)

    $bikePen = New-Object System.Drawing.Pen((New-Color "#F7E9FF"), [Math]::Max(3, $Size * 0.033))
    $bikePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $bikePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $bikePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $wheelSize = $Size * 0.22
    $leftWheel = [System.Drawing.RectangleF]::new($Size * 0.23, $Size * 0.50, $wheelSize, $wheelSize)
    $rightWheel = [System.Drawing.RectangleF]::new($Size * 0.55, $Size * 0.50, $wheelSize, $wheelSize)

    $graphics.DrawEllipse($bikePen, $leftWheel)
    $graphics.DrawEllipse($bikePen, $rightWheel)

    $seatPoint = [System.Drawing.PointF]::new($Size * 0.41, $Size * 0.36)
    $bottomBracketPoint = [System.Drawing.PointF]::new($Size * 0.51, $Size * 0.59)
    $headTubePoint = [System.Drawing.PointF]::new($Size * 0.63, $Size * 0.39)
    $rearAxlePoint = [System.Drawing.PointF]::new($leftWheel.X + ($wheelSize * 0.5), $leftWheel.Y + ($wheelSize * 0.5))
    $frontAxlePoint = [System.Drawing.PointF]::new($rightWheel.X + ($wheelSize * 0.5), $rightWheel.Y + ($wheelSize * 0.5))

    $graphics.DrawLine($bikePen, $rearAxlePoint, $seatPoint)
    $graphics.DrawLine($bikePen, $seatPoint, $headTubePoint)
    $graphics.DrawLine($bikePen, $headTubePoint, $bottomBracketPoint)
    $graphics.DrawLine($bikePen, $bottomBracketPoint, $rearAxlePoint)
    $graphics.DrawLine($bikePen, $bottomBracketPoint, $frontAxlePoint)
    $graphics.DrawLine($bikePen, $headTubePoint, $frontAxlePoint)

    $handlebarPen = New-Object System.Drawing.Pen((New-Color "#4CEB6E"), [Math]::Max(2, $Size * 0.02))
    $handlebarPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handlebarPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($handlebarPen, $Size * 0.60, $Size * 0.31, $Size * 0.69, $Size * 0.27)
    $graphics.DrawLine($handlebarPen, $Size * 0.60, $Size * 0.31, $Size * 0.65, $Size * 0.36)
    $graphics.DrawLine($handlebarPen, $Size * 0.36, $Size * 0.34, $Size * 0.46, $Size * 0.34)

    $accentPen = New-Object System.Drawing.Pen((New-Color "#FF95D9"), [Math]::Max(2, $Size * 0.015))
    $accentPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $accentPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($accentPen, $Size * 0.29, $Size * 0.28, $Size * 0.72, $Size * 0.28)

    $graphics.Dispose()
    return $bitmap
}

function Convert-BitmapToPngBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $stream = New-Object System.IO.MemoryStream
    $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return $bytes
}

$resolvedRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outputFile = Join-Path $resolvedRoot.Path $OutputPath
$outputDirectory = Split-Path -Parent $outputFile
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngPayloads = @()

foreach ($size in $sizes) {
    $bitmap = Draw-VeloCenterIcon -Size $size
    try {
        $pngPayloads += ,@($size, (Convert-BitmapToPngBytes -Bitmap $bitmap))
    }
    finally {
        $bitmap.Dispose()
    }
}

$fileStream = [System.IO.File]::Open($outputFile, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter($fileStream)

try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$pngPayloads.Count)

    $offset = 6 + (16 * $pngPayloads.Count)

    foreach ($entry in $pngPayloads) {
        $size = [int]$entry[0]
        $bytes = [byte[]]$entry[1]

        $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
        $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$bytes.Length)
        $writer.Write([UInt32]$offset)

        $offset += $bytes.Length
    }

    foreach ($entry in $pngPayloads) {
        $writer.Write([byte[]]$entry[1])
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}

Write-Host "Generated icon at $outputFile"
