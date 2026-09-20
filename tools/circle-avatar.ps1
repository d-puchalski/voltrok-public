param(
    [string]$InputPath,
    [string]$OutputPath,
    [string]$InputDirectory,
    [string]$OutputDirectory,

    [int]$Size = 256,
    [switch]$AllowUpscale,
    [int]$BorderWidth = 0,
    [string]$BorderColor = "#94A3B8",
    [bool]$OptimizePng = $true,
    [int]$PngQualityMin = 55,
    [int]$PngQualityMax = 65,
    [int]$PngSpeed = 2,
    [string]$PngQuantPath,
    [switch]$Recurse
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

if ($PngQualityMin -lt 0 -or $PngQualityMin -gt 100) { throw "PngQualityMin must be 0..100" }
if ($PngQualityMax -lt 0 -or $PngQualityMax -gt 100) { throw "PngQualityMax must be 0..100" }
if ($PngQualityMin -gt $PngQualityMax) { throw "PngQualityMin must be <= PngQualityMax" }
if ($PngSpeed -lt 1 -or $PngSpeed -gt 11) { throw "PngSpeed must be 1..11" }

$script:PngQuantPath = $null
if ($OptimizePng) {
    if ($PngQuantPath) {
        if (Test-Path -LiteralPath $PngQuantPath) {
            $script:PngQuantPath = (Resolve-Path -LiteralPath $PngQuantPath).Path
        } else {
            Write-Warning "Provided PngQuantPath does not exist: $PngQuantPath"
        }
    }

    if (-not $script:PngQuantPath) {
        $pngQuantCmd = Get-Command pngquant -ErrorAction SilentlyContinue
        if ($pngQuantCmd) {
            $script:PngQuantPath = $pngQuantCmd.Source
        }
    }

    if (-not $script:PngQuantPath) {
        $scriptDir = Split-Path -Parent $PSCommandPath
        $localCandidates = @(
            (Join-Path $scriptDir "pngquant.exe"),
            (Join-Path $scriptDir "pngquant\\pngquant.exe")
        )

        foreach ($candidate in $localCandidates) {
            if (Test-Path -LiteralPath $candidate) {
                $script:PngQuantPath = (Resolve-Path -LiteralPath $candidate).Path
                break
            }
        }
    }

    if ($script:PngQuantPath) {
        Write-Host "Using pngquant: $script:PngQuantPath"
    } else {
        Write-Warning "pngquant not found in PATH. PNG quality optimization skipped."
    }
}

function Get-RelativePathSafe {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    try {
        return [System.IO.Path]::GetRelativePath($BasePath, $TargetPath)
    }
    catch {
        $baseFull = [System.IO.Path]::GetFullPath($BasePath)
        if (-not $baseFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
            $baseFull += [System.IO.Path]::DirectorySeparatorChar
        }

        $targetFull = [System.IO.Path]::GetFullPath($TargetPath)
        $baseUri = New-Object System.Uri($baseFull)
        $targetUri = New-Object System.Uri($targetFull)
        $relativeUri = $baseUri.MakeRelativeUri($targetUri)
        return [System.Uri]::UnescapeDataString($relativeUri.ToString().Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    }
}

function Convert-ToCircularAvatar {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $targetDir = Split-Path -Parent $TargetPath
    if ($targetDir -and -not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir | Out-Null
    }

    $targetExt = [System.IO.Path]::GetExtension($TargetPath).ToLowerInvariant()
    if ($targetExt -in @(".jpg", ".jpeg")) {
        Write-Warning "Transparency requires alpha channel. Converting output to PNG: $TargetPath"
        $TargetPath = [System.IO.Path]::ChangeExtension($TargetPath, ".png")
        $targetExt = ".png"
    }

    $srcImage = [System.Drawing.Image]::FromFile($SourcePath)
    try {
        $srcW = $srcImage.Width
        $srcH = $srcImage.Height
        $cropSize = [Math]::Min($srcW, $srcH)

        $targetSize = if ($Size -gt 0) { $Size } else { $cropSize }
        if (-not $AllowUpscale -and $targetSize -gt $cropSize) {
            $targetSize = $cropSize
        }

        $bmp = New-Object System.Drawing.Bitmap($targetSize, $targetSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            try {
                $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $g.Clear([System.Drawing.Color]::Transparent)

                $srcX = [int](($srcW - $cropSize) / 2)
                $srcY = [int](($srcH - $cropSize) / 2)
                $srcRect = New-Object System.Drawing.Rectangle($srcX, $srcY, $cropSize, $cropSize)

                $diameter = $targetSize - 2
                $clipPath = New-Object System.Drawing.Drawing2D.GraphicsPath
                try {
                    $clipPath.AddEllipse(1, 1, $diameter, $diameter)
                    $g.SetClip($clipPath)
                    $dstRect = New-Object System.Drawing.Rectangle(0, 0, $targetSize, $targetSize)
                    $g.DrawImage($srcImage, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
                    $g.ResetClip()
                }
                finally {
                    $clipPath.Dispose()
                }

                if ($BorderWidth -gt 0) {
                    $penColor = [System.Drawing.ColorTranslator]::FromHtml($BorderColor)
                    $pen = New-Object System.Drawing.Pen($penColor, $BorderWidth)
                    try {
                        $offset = [int]([Math]::Ceiling($BorderWidth / 2.0))
                        $strokeSize = $targetSize - ($offset * 2)
                        if ($strokeSize -gt 0) {
                            $g.DrawEllipse($pen, $offset, $offset, $strokeSize, $strokeSize)
                        }
                    }
                    finally {
                        $pen.Dispose()
                    }
                }
            }
            finally {
                $g.Dispose()
            }

            switch ($targetExt) {
                ".png"  { $bmp.Save($TargetPath, [System.Drawing.Imaging.ImageFormat]::Png); break }
                ".bmp"  { $bmp.Save($TargetPath, [System.Drawing.Imaging.ImageFormat]::Bmp); break }
                default { $bmp.Save($TargetPath, [System.Drawing.Imaging.ImageFormat]::Png); break }
            }
        }
        finally {
            $bmp.Dispose()
        }
    }
    finally {
        $srcImage.Dispose()
    }

    return $TargetPath
}

function Optimize-PngIfPossible {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

    if (-not $OptimizePng) { return }
    if (-not $script:PngQuantPath) { return }
    if ([System.IO.Path]::GetExtension($Path).ToLowerInvariant() -ne ".png") { return }
    if (-not (Test-Path -LiteralPath $Path)) { return }

    $tmpPath = [System.IO.Path]::Combine(
        [System.IO.Path]::GetDirectoryName($Path),
        ([System.IO.Path]::GetFileNameWithoutExtension($Path) + ".tmp.png"))

    & $script:PngQuantPath `
        --force `
        --skip-if-larger `
        --strip `
        --quality="$PngQualityMin-$PngQualityMax" `
        --speed $PngSpeed `
        --output $tmpPath `
        -- $Path | Out-Null

    if (Test-Path -LiteralPath $tmpPath) {
        Move-Item -LiteralPath $tmpPath -Destination $Path -Force
    }
}

$imageExtensions = @(".png", ".jpg", ".jpeg", ".webp", ".bmp")

if ($InputDirectory) {
    if (-not $OutputDirectory) {
        throw "When using -InputDirectory, you must provide -OutputDirectory."
    }
    if (-not (Test-Path -LiteralPath $InputDirectory)) {
        throw "Input directory not found: $InputDirectory"
    }

    if (-not (Test-Path -LiteralPath $OutputDirectory)) {
        New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    }

    $inputFull = [System.IO.Path]::GetFullPath($InputDirectory)
    $outputFull = [System.IO.Path]::GetFullPath($OutputDirectory)
    if (-not $outputFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $outputFull += [System.IO.Path]::DirectorySeparatorChar
    }

    $files = @(
        Get-ChildItem -Path $InputDirectory -File -Recurse:$Recurse |
        Where-Object {
            $extOk = $imageExtensions -contains $_.Extension.ToLowerInvariant()
            if (-not $extOk) { return $false }

            $fileFull = [System.IO.Path]::GetFullPath($_.FullName)
            $isInsideOutput = $fileFull.StartsWith($outputFull, [System.StringComparison]::OrdinalIgnoreCase)
            return -not $isInsideOutput
        }
    )

    foreach ($file in $files) {
        $relativePath = Get-RelativePathSafe -BasePath $inputFull -TargetPath $file.FullName
        $targetPath = Join-Path $OutputDirectory $relativePath
        $savedPath = Convert-ToCircularAvatar -SourcePath $file.FullName -TargetPath $targetPath
        Optimize-PngIfPossible -Path $savedPath
        Write-Host "Saved circular avatar to: $savedPath"
    }

    Write-Host "Processed $($files.Count) file(s)."
    exit 0
}

if (-not $InputPath -or -not $OutputPath) {
    throw "Use either (-InputPath and -OutputPath) for one file, or (-InputDirectory and -OutputDirectory) for batch mode."
}

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Input file not found: $InputPath"
}

$savedPath = Convert-ToCircularAvatar -SourcePath $InputPath -TargetPath $OutputPath
Optimize-PngIfPossible -Path $savedPath
Write-Host "Saved circular avatar to: $savedPath"
