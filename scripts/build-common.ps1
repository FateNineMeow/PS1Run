#Requires -Version 5.1
<#
.SYNOPSIS
    Ps1Run 构建脚本的共用实现，由 build-all.ps1 / build-net48.ps1 / build-net11.ps1 调用。

.DESCRIPTION
    一般不直接运行。统一项目路径、dotnet 参数、退出码与单测入口，
    避免三个构建入口各自复制一份逻辑后行为漂移。
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:Ps1RunRoot = Split-Path -Parent $PSScriptRoot

function Invoke-Ps1RunBuild {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("All", "Net48", "Net11")]
        [string] $Target,

        [ValidateSet("Debug", "Release")]
        [string] $Configuration = "Debug",

        # 跳过 NuGet 还原（依赖已恢复时可加快构建）
        [switch] $NoRestore,

        # 构建成功后运行 Ps1Run.Core.Tests（4/4 组）
        [switch] $Test
    )

    $root = $script:Ps1RunRoot
    $solution = Join-Path $root "Ps1Run.slnx"
    $appProject = Join-Path $root "Ps1Run.App\Ps1Run.App.csproj"
    $testProject = Join-Path $root "Ps1Run.Core.Tests\Ps1Run.Core.Tests.csproj"

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "错误：未找到 dotnet 命令，请先安装 .NET SDK。" -ForegroundColor Red
        return 1
    }

    $arguments = @()
    $label = ""

    switch ($Target) {
        "All" {
            # 解决方案包含 Core/App/Tests；一次构建覆盖双目标
            $arguments = @("build", $solution, "-c", $Configuration)
            $label = "全部（net48 + net11.0-windows）"
        }
        "Net48" {
            # 只构建 App 的 net48，ProjectReference 会按同目标构建 Core
            $arguments = @("build", $appProject, "-c", $Configuration, "-f", "net48")
            $label = "net48"
        }
        "Net11" {
            $arguments = @("build", $appProject, "-c", $Configuration, "-f", "net11.0-windows")
            $label = "net11.0-windows"
        }
    }

    if ($NoRestore) {
        $arguments += "--no-restore"
    }

    Write-Host "===== Ps1Run 构建：$label / $Configuration =====" -ForegroundColor Cyan
    Write-Host "执行：dotnet $($arguments -join ' ')" -ForegroundColor Gray

    # dotnet 输出为 UTF-8；Windows PowerShell 5.1 控制台默认按 GBK 解码会乱码
    $previousEncoding = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [System.Text.Encoding]::UTF8

        # Out-Host：让构建日志直接显示，而不是混进函数返回值（返回值只保留退出码）
        & dotnet @arguments | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build 失败（exit code $LASTEXITCODE）"
        }

        if ($Test) {
            Write-Host "===== 运行单元测试 =====" -ForegroundColor Cyan
            & dotnet run --project $testProject -f net11.0-windows -c $Configuration | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "单元测试失败（exit code $LASTEXITCODE）"
            }
        }

        Write-Host "构建完成：$label" -ForegroundColor Green
        Write-Host "产物目录：$(Join-Path $root 'Build')" -ForegroundColor Green
        return 0
    }
    catch {
        Write-Host "构建失败：$($_.Exception.Message)" -ForegroundColor Red
        return 1
    }
    finally {
        [Console]::OutputEncoding = $previousEncoding
    }
}
