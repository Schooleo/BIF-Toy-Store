[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CompilerPath,

    [Parameter(Mandatory = $true)]
    [string]$ScriptPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedCompilerPath = [System.IO.Path]::GetFullPath($CompilerPath)
$resolvedScriptPath = [System.IO.Path]::GetFullPath($ScriptPath)

if (-not (Test-Path -LiteralPath $resolvedCompilerPath)) {
    throw "ISCC.exe not found: $resolvedCompilerPath"
}

if (-not (Test-Path -LiteralPath $resolvedScriptPath)) {
    throw "Inno Setup script not found: $resolvedScriptPath"
}

Write-Host "Validating ISCC executable: $resolvedCompilerPath"
& $resolvedCompilerPath /? | Out-Host

if ($LASTEXITCODE -ne 0) {
    throw "ISCC help command failed with exit code $LASTEXITCODE"
}

Write-Host "ISCC validation passed for script: $resolvedScriptPath"
