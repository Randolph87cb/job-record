[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version,
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

function Get-ShortGitSha {
    $sha = git rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sha)) {
        throw "无法获取当前提交短 SHA。"
    }

    return $sha.Trim()
}

function Get-TaggedVersion {
    $tags = @(git tag --points-at HEAD 2>$null | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
    if ($LASTEXITCODE -ne 0) {
        throw "无法读取当前提交的 Git tag。"
    }

    $versionTags = @($tags | Where-Object { $_ -match '^v?\d+\.\d+\.\d+([\-][0-9A-Za-z.-]+)?$' })
    if ($versionTags.Count -eq 0) {
        return $null
    }

    if ($versionTags.Count -gt 1) {
        throw "当前提交上存在多个可用版本 tag：$($versionTags -join ', ')。请保留一个后重试，或显式传入 -Version。"
    }

    return $versionTags[0]
}

function Resolve-ReleaseVersion {
    param(
        [string]$ExplicitVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitVersion)) {
        return $ExplicitVersion.Trim()
    }

    $tagVersion = Get-TaggedVersion
    if (-not [string]::IsNullOrWhiteSpace($tagVersion)) {
        return $tagVersion
    }

    throw "未提供 -Version，且当前提交没有精确版本 tag。请先传入 -Version，或给当前提交打上 v1.2.3 / 1.2.3 形式的 tag。"
}

function ConvertTo-VersionMetadata {
    param(
        [Parameter(Mandatory)]
        [string]$RawVersion
    )

    if ($RawVersion -notmatch '^v?(?<core>\d+\.\d+\.\d+)(?<suffix>-[0-9A-Za-z.-]+)?$') {
        throw "版本号格式无效：$RawVersion。仅支持 1.2.3、v1.2.3、1.2.3-beta.1、v1.2.3-beta.1。"
    }

    $displayVersion = $RawVersion.TrimStart('v')
    $coreVersion = $matches['core']
    $assemblyVersion = "$coreVersion.0"
    $shortSha = Get-ShortGitSha
    $informationalVersion = "$displayVersion+sha.$shortSha"

    return [PSCustomObject]@{
        DisplayVersion = $displayVersion
        InstallerVersion = $assemblyVersion
        AssemblyVersion = $assemblyVersion
        FileVersion = $assemblyVersion
        InformationalVersion = $informationalVersion
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\JobRecord.App\JobRecord.App.csproj"
$issPath = Join-Path $repoRoot "installer\JobRecord.iss"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$installerOutputDir = Join-Path $repoRoot "artifacts\installer"
$dotnetPath = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
$installScriptPath = Join-Path $PSScriptRoot "install-inno-setup.ps1"
$iconPath = Join-Path $repoRoot "assets\branding\JobRecord.ico"

if (-not (Test-Path $projectPath)) {
    throw "找不到项目文件：$projectPath"
}

if (-not (Test-Path $issPath)) {
    throw "找不到 Inno Setup 脚本：$issPath"
}

if (-not (Test-Path $iconPath)) {
    throw "找不到安装器图标：$iconPath"
}

if (-not (Test-Path $dotnetPath)) {
    $dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        $dotnetPath = $dotnetCommand.Source
    } else {
        throw "找不到 dotnet.exe，请先准备 .NET SDK。"
    }
}

$resolvedVersion = Resolve-ReleaseVersion -ExplicitVersion $Version
$versionMetadata = ConvertTo-VersionMetadata -RawVersion $resolvedVersion

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

Write-Host "发布版本：" $versionMetadata.DisplayVersion
Write-Host "发布应用到：" $publishDir
& $dotnetPath publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:Version=$($versionMetadata.DisplayVersion) `
    -p:AssemblyVersion=$($versionMetadata.AssemblyVersion) `
    -p:FileVersion=$($versionMetadata.FileVersion) `
    -p:InformationalVersion=$($versionMetadata.InformationalVersion) `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败，退出码：$LASTEXITCODE"
}

Write-Host "编译 Inno Setup 安装包..."
& $isccPath `
    "/DAppVersion=$($versionMetadata.DisplayVersion)" `
    "/DInstallerVersion=$($versionMetadata.InstallerVersion)" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$installerOutputDir" `
    "/DIconFile=$iconPath" `
    $issPath

if ($LASTEXITCODE -ne 0) {
    throw "ISCC 编译失败，退出码：$LASTEXITCODE"
}

$installerPath = Join-Path $installerOutputDir "JobRecord-Setup-$($versionMetadata.DisplayVersion).exe"
if (-not (Test-Path $installerPath)) {
    throw "安装包生成完成，但未找到期望输出：$installerPath"
}

Write-Host ""
Write-Host "安装包已生成：" $installerPath
