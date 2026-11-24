#Requires -Version 5.1
<#
.SYNOPSIS
    只构建 net11.0-windows 目标（产物 Build\net11.0-windows\Ps1Run.exe + Ps1Run.dll）。

.EXAMPLE
    .\scripts\build-net11.ps1
    .\scripts\build-net11.ps1 -Configuration Release -Test
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [switch] $NoRestore,

    [switch] $Test
)

. (Join-Path $PSScriptRoot "build-common.ps1")
exit (Invoke-Ps1RunBuild -Target Net11 -Configuration $Configuration -NoRestore:$NoRestore -Test:$Test)
