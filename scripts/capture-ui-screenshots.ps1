param(
    [string]$Configuration = "Debug",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts\ui-screenshots"
}

$dotnetPath = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
$solutionPath = Join-Path $repoRoot "JobRecord.sln"

& $dotnetPath build $solutionPath -c $Configuration | Out-Host

$exePath = Join-Path $repoRoot "src\JobRecord.App\bin\$Configuration\net8.0-windows\JobRecord.App.exe"
if (-not (Test-Path $exePath)) {
    throw "未找到可执行文件：$exePath"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Get-ChildItem -Path $OutputDir -Filter *.png -ErrorAction SilentlyContinue | Remove-Item -Force

Add-Type -AssemblyName System.Drawing

function Wait-PreviewScreenshot {
    param(
        [string]$Path,
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 20
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $Path) {
            $item = Get-Item $Path
            if ($item.Length -gt 0) {
                return
            }
        }

        if ($Process.HasExited) {
            break
        }

        Start-Sleep -Milliseconds 200
    }

    if (-not (Test-Path $Path)) {
        throw "等待截图生成超时：$Path"
    }
}

function Trim-TransparentMargins {
    param(
        [string]$Path,
        [int]$Padding = 4
    )

    $source = [System.Drawing.Bitmap]::FromFile($Path)
    $tempPath = "$Path.trim.png"
    try {
        $left = $source.Width
        $top = $source.Height
        $right = -1
        $bottom = -1

        for ($y = 0; $y -lt $source.Height; $y++) {
            for ($x = 0; $x -lt $source.Width; $x++) {
                $pixel = $source.GetPixel($x, $y)
                if ($pixel.A -gt 0) {
                    if ($x -lt $left) { $left = $x }
                    if ($x -gt $right) { $right = $x }
                    if ($y -lt $top) { $top = $y }
                    if ($y -gt $bottom) { $bottom = $y }
                }
            }
        }

        if ($right -lt $left -or $bottom -lt $top) {
            return
        }

        $cropLeft = [Math]::Max(0, $left - $Padding)
        $cropTop = [Math]::Max(0, $top - $Padding)
        $cropWidth = [Math]::Min($source.Width - $cropLeft, ($right - $left + 1) + ($Padding * 2))
        $cropHeight = [Math]::Min($source.Height - $cropTop, ($bottom - $top + 1) + ($Padding * 2))

        $cropped = New-Object System.Drawing.Bitmap($cropWidth, $cropHeight)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($cropped)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $sourceRect = New-Object System.Drawing.Rectangle($cropLeft, $cropTop, $cropWidth, $cropHeight)
                $targetRect = New-Object System.Drawing.Rectangle(0, 0, $cropWidth, $cropHeight)
                $graphics.DrawImage($source, $targetRect, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $graphics.Dispose()
            }

            $cropped.Save($tempPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $cropped.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }

    if (Test-Path $Path) {
        Remove-Item -Force -Path $Path
    }

    Move-Item -Force -Path $tempPath -Destination $Path
}

$scenarios = @(
    @{ Name = "top-collapsed"; Dock = "top"; Expanded = "false"; Compact = "true";  State = "running" },
    @{ Name = "top-expanded";  Dock = "top"; Expanded = "true";  Compact = "false"; State = "running" },
    @{ Name = "left-collapsed"; Dock = "left"; Expanded = "false"; Compact = "false"; State = "running" },
    @{ Name = "left-expanded";  Dock = "left"; Expanded = "true";  Compact = "false"; State = "running" },
    @{ Name = "right-collapsed"; Dock = "right"; Expanded = "false"; Compact = "false"; State = "paused" },
    @{ Name = "right-expanded";  Dock = "right"; Expanded = "true";  Compact = "false"; State = "paused" }
)

foreach ($scenario in $scenarios) {
    $outputPath = Join-Path $OutputDir "$($scenario.Name).png"
    $arguments = @(
        "--preview",
        "--dock=$($scenario.Dock)",
        "--expanded=$($scenario.Expanded)",
        "--compact=$($scenario.Compact)",
        "--state=$($scenario.State)",
        "--screenshot-path=$outputPath"
    )

    $process = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $repoRoot -PassThru
    try {
        Wait-PreviewScreenshot -Path $outputPath -Process $process
        $process.WaitForExit()
        Trim-TransparentMargins -Path $outputPath
        Write-Host "已生成 $outputPath"
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}
