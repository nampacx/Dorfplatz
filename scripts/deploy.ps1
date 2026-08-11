<#
.SYNOPSIS
  Local end-to-end deploy without CI: provisions Azure with Bicep, publishes the
  Function App, and uploads the frontend to Static Web Apps. Prints the site URL.

.EXAMPLE
  ./scripts/deploy.ps1 -SubscriptionId <sub> -SeedAdminEmail you@example.com

.NOTES
  Requires: az, .NET 8 SDK, Node 20+ (npx). PostgreSQL admin password is generated
  if not supplied (store it). Re-running resets the DB admin password to the value used.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $SubscriptionId,
  [string] $ResourceGroup = 'bb-dev-rg',
  [string] $Location = 'westeurope',
  [ValidateSet('dev','prod')] [string] $Environment = 'dev',
  [string] $PostgresPassword,
  [string] $SeedAdminEmail = '',
  [switch] $SkipInfra
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Info($m) { Write-Host "==> $m" -ForegroundColor Cyan }

az account show 1>$null 2>$null; if ($LASTEXITCODE) { throw 'Run "az login" first.' }
az account set --subscription $SubscriptionId
$deployerObjectId = az ad signed-in-user show --query id -o tsv

if (-not $PostgresPassword) {
  $PostgresPassword = -join ((48..57)+(65..90)+(97..122)+(35,64,37) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
  Info "Generated PostgreSQL admin password: $PostgresPassword  (store this)"
}

# --- 1. Provision ---------------------------------------------------------
if (-not $SkipInfra) {
  Info 'Provisioning infrastructure (Bicep)...'
  az group create -n $ResourceGroup -l $Location 1>$null
}
$outputs = az deployment group create -g $ResourceGroup `
  --name "bb-$Environment-local" `
  --template-file "$root/infra/main.bicep" `
  --parameters `
    environmentName=$Environment `
    postgresAdminPassword=$PostgresPassword `
    deployerObjectId=$deployerObjectId `
    seedAdminEmail=$SeedAdminEmail `
  --query properties.outputs -o json | ConvertFrom-Json

$funcName = $outputs.functionAppName.value
$swaName  = $outputs.staticWebAppName.value
$swaHost  = $outputs.staticWebAppHostname.value
Info "Function App: $funcName    Static Web App: $swaName"

# --- 2. Deploy API (zip) --------------------------------------------------
Info 'Publishing the Function App...'
$publish = Join-Path $env:TEMP "bb-api-publish"
$zip = Join-Path $env:TEMP "bb-api.zip"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish "$root/api/BulletinBoard.Api.csproj" -c Release -o $publish 1>$null
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$publish/*" -DestinationPath $zip
az functionapp deployment source config-zip -g $ResourceGroup -n $funcName --src $zip 1>$null

# --- 3. Deploy frontend (SWA) --------------------------------------------
Info 'Building and deploying the frontend...'
Push-Location "$root/frontend"
try {
  if (-not (Test-Path node_modules)) { npm ci 1>$null }
  $env:VITE_API_BASE = '/api'
  npm run build 1>$null
  $token = az staticwebapp secrets list --name $swaName --query 'properties.apiKey' -o tsv
  npx --yes @azure/static-web-apps-cli deploy ./dist --deployment-token $token --env production 1>$null
} finally { Pop-Location }

Write-Host ''
Info "Done. Your app: https://$swaHost"
Write-Host "Next: ./scripts/setup-entra-auth.ps1 -Repo <owner/repo> -ResourceGroup $ResourceGroup" -ForegroundColor Yellow
