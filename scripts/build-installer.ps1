[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0",
    [switch]$Clean
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

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\JobRecord.App\JobRecord.App.csproj"
$issPath = Join-Path $repoRoot "installer\JobRecord.iss"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$installerOutputDir = Join-Path $repoRoot "artifacts\installer"
$dotnetPath = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
$installScriptPath = Join-Path $PSScriptRoot "install-inno-setup.ps1"

if (-not (Test-Path $projectPath)) {
    throw "找不到项目文件：$projectPath"
}

if (-not (Test-Path $issPath)) {
    throw "找不到 Inno Setup 脚本：$issPath"
}

if (-not (Test-Path $dotnetPath)) {
    $dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        $dotnetPath = $dotnetCommand.Source
    } else {
        throw "找不到 dotnet.exe，请先准备 .NET SDK。"
    }
}

if ($Clean) {
    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }

    if (Test-Path $installerOutputDir) {
        Remove-Item -Recurse -Force $installerOutputDir
    }
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerOutputDir | Out-Null

$isccPath = Resolve-IsccPath
if (-not $isccPath) {
    if (-not (Test-Path $installScriptPath)) {
        throw "未找到 ISCC.exe，且缺少自动安装脚本：$installScriptPath"
    }

    Write-Host "未检测到 Inno Setup，开始自动安装..."
    & $installScriptPath
    $isccPath = Resolve-IsccPath
}

if (-not $isccPath) {
    throw "未找到 ISCC.exe，无法继续生成安装包。"
}

Write-Host "发布应用到：" $publishDir
& $dotnetPath publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败，退出码：$LASTEXITCODE"
}

Write-Host "编译 Inno Setup 安装包..."
& $isccPath `
    "/DAppVersion=$Version" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$installerOutputDir" `
    $issPath

if ($LASTEXITCODE -ne 0) {
    throw "ISCC 编译失败，退出码：$LASTEXITCODE"
}

$installerPath = Join-Path $installerOutputDir "JobRecord-Setup-$Version.exe"
if (-not (Test-Path $installerPath)) {
    throw "安装包生成完成，但未找到期望输出：$installerPath"
}

Write-Host ""
Write-Host "安装包已生成：" $installerPath
