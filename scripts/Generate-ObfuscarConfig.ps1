[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $true)]
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return [System.IO.Path]::GetFullPath($Path)
}

$resolvedInputDir = Resolve-FullPath -Path $InputDir
$resolvedOutputDir = Resolve-FullPath -Path $OutputDir
$resolvedConfigPath = Resolve-FullPath -Path $ConfigPath
$resolvedConfigDir = Split-Path -Path $resolvedConfigPath -Parent
$logPath = Join-Path $resolvedOutputDir 'obfuscar-log.xml'

if (-not (Test-Path -LiteralPath $resolvedInputDir)) {
    throw "Input publish directory not found: $resolvedInputDir"
}

New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $resolvedConfigDir | Out-Null

$modules = @(
    @{
        FileName = 'BIF.ToyStore.Core.dll'
        Rules = @()
    },
    @{
        FileName = 'BIF.ToyStore.Infrastructure.dll'
        Rules = @(
            '  <SkipType name="BIF.ToyStore.Infrastructure.GraphQL.CreateProductInput" skipProperties="true" />',
            '  <SkipType name="BIF.ToyStore.Infrastructure.GraphQL.UpdateProductInput" skipProperties="true" />',
            '  <SkipType name="BIF.ToyStore.Infrastructure.GraphQL.CreateCategoryInput" skipProperties="true" />',
            '  <SkipType name="BIF.ToyStore.Infrastructure.GraphQL.UpdateCategoryInput" skipProperties="true" />'
        )
    },
    @{
        FileName = 'BIF.ToyStore.ViewModels.dll'
        Rules = @()
    },
    @{
        FileName = 'BIF.ToyStore.WinUI.dll'
        Rules = @(
            '  <SkipNamespace name="BIF.ToyStore.WinUI.Views*" />',
            '  <SkipNamespace name="BIF.ToyStore.WinUI.Controls*" />',
            '  <SkipNamespace name="BIF.ToyStore.WinUI.Converters*" />',
            '  <SkipType name="BIF.ToyStore.WinUI.App" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" skipStringHiding="true" />',
            '  <SkipType name="BIF.ToyStore.WinUI.MainWindow" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" skipStringHiding="true" />'
        )
    }
)

foreach ($module in $modules) {
    $modulePath = Join-Path $resolvedInputDir $module.FileName
    if (-not (Test-Path -LiteralPath $modulePath)) {
        throw "Expected module not found for obfuscation: $modulePath"
    }
}

$settings = @(
    @{ Name = 'InPath'; Value = $resolvedInputDir },
    @{ Name = 'OutPath'; Value = $resolvedOutputDir },
    @{ Name = 'LogFile'; Value = $logPath },
    @{ Name = 'KeepPublicApi'; Value = 'true' },
    @{ Name = 'HidePrivateApi'; Value = 'true' },
    @{ Name = 'SkipGenerated'; Value = 'true' },
    @{ Name = 'SkipSpecialName'; Value = 'true' }
)

function Escape-XmlAttribute {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return [System.Security.SecurityElement]::Escape($Value)
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('<?xml version="1.0" encoding="utf-8"?>')
$lines.Add('<Obfuscator>')

foreach ($setting in $settings) {
    $lines.Add("  <Var name=""$(Escape-XmlAttribute $setting.Name)"" value=""$(Escape-XmlAttribute $setting.Value)"" />")
}

foreach ($module in $modules) {
    $modulePath = Join-Path $resolvedInputDir $module.FileName
    if ($module.Rules.Count -eq 0) {
        $lines.Add("  <Module file=""$(Escape-XmlAttribute $modulePath)"" />")
        continue
    }

    $lines.Add("  <Module file=""$(Escape-XmlAttribute $modulePath)"">")
    foreach ($rule in $module.Rules) {
        $lines.Add($rule)
    }
    $lines.Add('  </Module>')
}

$lines.Add('</Obfuscator>')

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedConfigPath, ($lines -join [Environment]::NewLine), $utf8NoBom)

Write-Host "Generated Obfuscar config: $resolvedConfigPath"
Write-Host "InputDir: $resolvedInputDir"
Write-Host "OutputDir: $resolvedOutputDir"
Write-Host "Modules:"
$modules | ForEach-Object { Write-Host " - $($_.FileName)" }
