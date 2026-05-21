# Generates all PWA icon sizes from a source PNG (your app logo).
# Usage: .\resize-icons.ps1 -SourcePath "C:\path\to\your-logo.png"
param(
    [Parameter(Mandatory)][string]$SourcePath
)

Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile((Resolve-Path $SourcePath).Path)
$iconsDir = Join-Path $PSScriptRoot "wwwroot\icons"
New-Item -ItemType Directory -Path $iconsDir -Force | Out-Null

function Resize-Image {
    param([System.Drawing.Image]$Img, [int]$Size, [string]$OutputPath)
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode  = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($Img, 0, 0, $Size, $Size)
    $g.Dispose()
    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "OK  $OutputPath"
}

# Standard icons
Resize-Image $src 192 "$iconsDir\icon-192.png"
Resize-Image $src 512 "$iconsDir\icon-512.png"
Resize-Image $src 180 "$iconsDir\apple-touch-icon.png"

# Maskable icon: logo fills inner 80% safe zone; navy background fills the rest.
# Chrome/Android may crop to a circle, so the logo must not extend to the edges.
$padded = New-Object System.Drawing.Bitmap(512, 512)
$g = [System.Drawing.Graphics]::FromImage($padded)
$g.Clear([System.Drawing.Color]::FromArgb(255, 10, 22, 40))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$pad      = [int](512 * 0.1)   # 10 % padding on each side = 80 % inner safe zone
$inner    = 512 - 2 * $pad
$g.DrawImage($src, $pad, $pad, $inner, $inner)
$g.Dispose()
$padded.Save("$iconsDir\icon-maskable-512.png", [System.Drawing.Imaging.ImageFormat]::Png)
$padded.Dispose()
Write-Host "OK  $iconsDir\icon-maskable-512.png"

$src.Dispose()
Write-Host "`nAll icons generated. Run 'dotnet build' to rebuild with updated assets."
