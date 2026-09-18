<#
.SYNOPSIS
  Creates a local Python environment for the Mitteilungsblatt PDF analyzer.

.EXAMPLE
  ./.github/skills/mitteilungsblatt/scripts/setup-python.ps1
#>
[CmdletBinding()]
param(
  [string] $Python = 'python',
  [string] $VenvPath
)

$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
if (-not $VenvPath) {
  $VenvPath = Join-Path $skillRoot '.venv'
}

Write-Host "Creating Python environment at $VenvPath"
& $Python -m venv $VenvPath
if ($LASTEXITCODE) { throw "Failed to create the Python environment with '$Python'." }

$venvPython = Join-Path $VenvPath 'Scripts/python.exe'
& $venvPython -m pip install --upgrade pip
if ($LASTEXITCODE) { throw 'Failed to upgrade pip.' }

& $venvPython -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt')
if ($LASTEXITCODE) { throw 'Failed to install PDF analysis dependencies.' }

Write-Host 'Environment ready.'
Write-Host "Run: & '$venvPython' '$PSScriptRoot/analyze_reference_pdf.py'"