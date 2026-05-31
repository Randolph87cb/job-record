[CmdletBinding()]
param(
    [string]$Version = "6.7.3"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-IsccPath {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 7\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
        (Join-Path $env:LOCALAPPDATA "Programs\Inno\ISCC.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $registryRoots = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    foreach ($registryRoot in $registryRoots) {
        $installLocation = Get-ItemProperty -Path $registryRoot -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "Inno Setup*" -and $_.InstallLocation } |
            Select-Object -First 1 -ExpandProperty InstallLocation

        if ($installLocation) {
            $registryCandidate = Join-Path $installLocation "ISCC.exe"
            if (Test-Path $registryCandidate) {
                return $registryCandidate
            }
        }
    }

    return $null
}

$existingPath = Resolve-IsccPath
if ($existingPath) {
    Write-Host "已检测到 Inno Setup：" $existingPath
    return
}

$releaseTag = "is-" + ($Version -replace "\.", "_")
$downloadUrl = "https://github.com/jrsoftware/issrc/releases/download/$releaseTag/innosetup-$Version.exe"
$tempDir = Join-Path $env:TEMP "JobRecord-InnoSetup"
$installerPath = Join-Path $tempDir "innosetup-$Version.exe"

New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

Write-Host "下载 Inno Setup $Version..."
Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath

Write-Host "静默安装 Inno Setup..."
$process = Start-Process -FilePath $installerPath -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-" -PassThru -Wait
if ($process.ExitCode -ne 0) {
    throw "Inno Setup 安装失败，退出码：$($process.ExitCode)"
}

$installedPath = Resolve-IsccPath
if (-not $installedPath) {
    throw "安装完成后仍未找到 ISCC.exe，请检查 Inno Setup 是否安装成功。"
}

Write-Host "Inno Setup 安装完成：" $installedPath
