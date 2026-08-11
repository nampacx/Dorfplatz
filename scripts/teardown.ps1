<#
.SYNOPSIS
  Deletes the resource group (and everything in it). Key Vault has purge protection,
  so optionally purge the soft-deleted vault afterwards to free the name.

.EXAMPLE
  ./scripts/teardown.ps1 -SubscriptionId <sub> -ResourceGroup bb-dev-rg
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $SubscriptionId,
  [string] $ResourceGroup = 'bb-dev-rg',
  [switch] $PurgeKeyVault
)

$ErrorActionPreference = 'Stop'
az account set --subscription $SubscriptionId

$kvName = az keyvault list -g $ResourceGroup --query '[0].name' -o tsv 2>$null

Write-Host "Deleting resource group '$ResourceGroup'..." -ForegroundColor Yellow
az group delete -n $ResourceGroup --yes 1>$null

if ($PurgeKeyVault -and $kvName) {
  Write-Host "Purging soft-deleted Key Vault '$kvName'..." -ForegroundColor Yellow
  az keyvault purge --name $kvName 1>$null 2>$null
}
Write-Host '==> Teardown complete.' -ForegroundColor Cyan
