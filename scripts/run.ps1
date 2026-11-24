#Requires -Version 5.1
<#
.SYNOPSIS
    运行 Ps1Run（默认启动 net48 产物）。

.DESCRIPTION
    先定位 Build\<TFM>\Ps1Run.exe，文件缺失时给出构建提示；
    加 -Build 可先调用共用构建实现再启动。
    脚本默认等待程序退出；如需直接传原始参数，用 -Arguments 数组。

.EXAMPLE
    .\scripts\run.ps1
    打开主界面（net48 / Debug）。

.EXAMPLE
    .\scripts\run.ps1 -Framework Net11 -Configuration Release
    运行 net11 / Release 产物。

.EXAMPLE
    .\scripts\run.ps1 -Build -Intent Run -ScriptPath "D:\scripts\中文 脚本.ps1"
    先构建，再强制运行指定脚本。

.EXAMPLE
    .\scripts\run.ps1 -Arguments @("--open", "D:\scripts\a.ps1")
    原样传递命令行参数给 Ps1Run。
#>

[CmdletBinding()]
param(
    [ValidateSet("Net48", "Net11")]
    [string] $Framework = "Net48",

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    # 启动前先构建所选目标
    [switch] $Build,

    [ValidateSet("Open", "Run", "Edit", "Ask", "Settings")]
    [string] $Intent,

    [string] $ScriptPath,

    [string[]] $Arguments = @()
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ($Build) {
    . (Join-Path $PSScriptRoot "build-common.ps1")
    $code = Invoke-Ps1RunBuild -Target $Framework -Configuration $Configuration
    if ($code -ne 0) {
        exit $code
    }
}

$relative = if ($Framework -eq "Net48") {
    "Build\net48\Ps1Run.exe"
}
else {
    "Build\net11.0-windows\Ps1Run.exe"
}
$exe = Join-Path $root $relative

if (-not (Test-Path $exe -PathType Leaf)) {
    $buildScript = "build-" + $Framework.ToLowerInvariant() + ".ps1"
    Write-Host "错误：找不到 $exe" -ForegroundColor Red
    Write-Host "请先运行 .\scripts\$buildScript，或给本脚本加 -Build 参数。" -ForegroundColor Yellow
    exit 1
}

$exeArgs = @()
if ($Intent) {
    $exeArgs += "--" + $Intent.ToLowerInvariant()
}
if ($ScriptPath) {
    if (-not (Test-Path $ScriptPath -PathType Leaf)) {
        Write-Host "错误：脚本文件不存在：$ScriptPath" -ForegroundColor Red
        exit 1
    }
    $exeArgs += (Resolve-Path $ScriptPath).Path
}
if ($Arguments.Count -gt 0) {
    $exeArgs += $Arguments
}

Write-Host "启动：$exe $($exeArgs -join ' ')" -ForegroundColor Cyan
& $exe @exeArgs
exit $LASTEXITCODE
