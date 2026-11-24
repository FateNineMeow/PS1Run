#Requires -Version 5.1
<#
.SYNOPSIS
    构建全部目标：net48 + net11.0-windows（含 Ps1Run.Core.Tests）。

.EXAMPLE
    .\scripts\build-all.ps1
    .\scripts\build-all.ps1 -Configuration Release -Test
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [switch] $NoRestore,

    [switch] $Test
)

. (Join-Path $PSScriptRoot "build-common.ps1")
exit (Invoke-Ps1RunBuild -Target All -Configuration $Configuration -NoRestore:$NoRestore -Test:$Test)
