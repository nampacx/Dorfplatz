<#
.SYNOPSIS
  Creates (or reuses) the Microsoft Entra app registration used for USER sign-in
  (personal + work Microsoft accounts), points its redirect URI at the deployed
  Static Web App, generates a client secret, and applies it to both the SWA app
  settings (effective immediately) and the GitHub secrets (for future deploys).

  Run this AFTER the first `deploy.yml` run, so the SWA hostname exists.

.EXAMPLE
  ./scripts/setup-entra-auth.ps1 -Repo nampacx/BulletinBoard -ResourceGroup bb-dev-rg
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Repo,
  [string] $ResourceGroup = 'bb-dev-rg',
  [string] $SwaName,
  [string] $AppName = 'BulletinBoard-Auth'
)

$ErrorActionPreference = 'Stop'
function Info($m) { Write-Host "==> $m" -ForegroundColor Cyan }

az account show 1>$null 2>$null; if ($LASTEXITCODE) { throw 'Run "az login" first.' }

if (-not $SwaName) {
  $SwaName = az staticwebapp list -g $ResourceGroup --query '[0].name' -o tsv
  if (-not $SwaName) { throw "No Static Web App found in $ResourceGroup. Deploy infra first." }
}
$host_ = az staticwebapp show -g $ResourceGroup -n $SwaName --query defaultHostname -o tsv
$redirect = "https://$host_/.auth/login/aad/callback"
Info "SWA: $SwaName ($host_)"

# --- App registration (personal + work accounts) --------------------------
$appId = az ad app list --display-name $AppName --query '[0].appId' -o tsv
if (-not $appId) {
  Info "Creating app registration '$AppName'..."
  $appId = az ad app create --display-name $AppName `
    --sign-in-audience AzureADandPersonalMicrosoftAccount `
    --web-redirect-uris $redirect --query appId -o tsv
} else {
  Info 'Updating redirect URI on existing app...'
  az ad app update --id $appId --web-redirect-uris $redirect 1>$null
}

Info 'Creating a client secret...'
$secret = az ad app credential reset --id $appId --append --display-name swa --query password -o tsv

# --- Apply to SWA (immediate) + GitHub (future deploys) -------------------
Info 'Applying settings to the Static Web App...'
az staticwebapp appsettings set -n $SwaName -g $ResourceGroup `
  --setting-names "ENTRA_CLIENT_ID=$appId" "ENTRA_CLIENT_SECRET=$secret" 1>$null

Info 'Storing GitHub secrets...'
$appId  | gh secret set ENTRA_CLIENT_ID     --repo $Repo 1>$null
$secret | gh secret set ENTRA_CLIENT_SECRET --repo $Repo 1>$null

Write-Host ''
Info 'Microsoft sign-in configured.'
Write-Host @"
Google (manual — no fully-scripted API):
  1. Google Cloud Console -> APIs & Services -> Credentials -> Create OAuth client ID (Web).
  2. Authorized redirect URI:  https://$host_/.auth/login/google/callback
  3. Then run:
       ./scripts/set-google-auth.ps1 -Repo $Repo -ResourceGroup $ResourceGroup ``
         -ClientId <id> -ClientSecret <secret>
"@
