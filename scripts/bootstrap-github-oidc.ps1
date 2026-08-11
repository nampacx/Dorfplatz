<#
.SYNOPSIS
  One-time bootstrap: creates the GitHub -> Azure OIDC deploy identity, assigns RBAC,
  creates the resource group, and pushes all GitHub Actions secrets/variables the
  deploy pipeline needs. After this, `deploy.yml` runs with zero manual wiring.

.EXAMPLE
  ./scripts/bootstrap-github-oidc.ps1 -SubscriptionId <sub> -Repo owner/BulletinBoard -SeedAdminEmail you@example.com

.NOTES
  Requires: Azure CLI (az) logged in with Owner/UAA on the subscription, and GitHub CLI (gh) logged in.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $SubscriptionId,
  [string] $Repo,                                        # e.g. nampacx/BulletinBoard; auto-detected from the current checkout if omitted
  [string] $ResourceGroup = 'bb-dev-rg',
  [string] $Location = 'westeurope',
  [string] $AppName = 'bb-github-deploy',
  [string[]] $Environments = @('dev', 'prod'),
  [string] $PostgresPassword,
  [string] $SeedAdminEmail = ''
)

$ErrorActionPreference = 'Stop'
function Info($m) { Write-Host "==> $m" -ForegroundColor Cyan }

# --- Preconditions --------------------------------------------------------
Info 'Checking prerequisites (az, gh)...'
az account show 1>$null 2>$null; if ($LASTEXITCODE) { throw 'Run "az login" first.' }
gh auth status 1>$null 2>$null; if ($LASTEXITCODE) { throw 'Run "gh auth login" first.' }

# Auto-detect the GitHub repo from the current checkout when not passed explicitly.
if (-not $Repo) {
  $Repo = gh repo view --json nameWithOwner -q .nameWithOwner 2>$null
  if (-not $Repo) { throw 'Could not detect the GitHub repo. Run inside the cloned repo or pass -Repo <owner>/<name>.' }
  Info "Detected repo: $Repo"
}

if (-not $PostgresPassword) {
  $PostgresPassword = -join ((48..57) + (65..90) + (97..122) + (33,35,37,64) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
  Info 'Generated a random PostgreSQL admin password.'
}

az account set --subscription $SubscriptionId
$tenantId = az account show --query tenantId -o tsv

# --- Resource group -------------------------------------------------------
Info "Ensuring resource group '$ResourceGroup' in $Location..."
az group create -n $ResourceGroup -l $Location 1>$null

# --- Deploy app registration + service principal --------------------------
Info "Creating app registration '$AppName'..."
$appId = az ad app list --display-name $AppName --query '[0].appId' -o tsv
if (-not $appId) { $appId = az ad app create --display-name $AppName --query appId -o tsv }

$spObjectId = az ad sp list --filter "appId eq '$appId'" --query '[0].id' -o tsv
if (-not $spObjectId) { $spObjectId = az ad sp create --id $appId --query id -o tsv }
Info "App (client) id: $appId"

# --- Federated credentials (main, PRs, environments) ----------------------
function Add-FederatedCredential($name, $subject) {
  $exists = az ad app federated-credential list --id $appId --query "[?name=='$name'] | [0].name" -o tsv
  if ($exists) { return }
  $cred = @{ name = $name; issuer = 'https://token.actions.githubusercontent.com'; subject = $subject; audiences = @('api://AzureADTokenExchange') }
  $tmp = New-TemporaryFile
  $cred | ConvertTo-Json | Set-Content $tmp
  az ad app federated-credential create --id $appId --parameters "@$tmp" 1>$null
  Remove-Item $tmp
  Info "Federated credential added: $subject"
}
Add-FederatedCredential 'gh-main'  "repo:${Repo}:ref:refs/heads/main"
Add-FederatedCredential 'gh-pr'    "repo:${Repo}:pull_request"
foreach ($e in $Environments) { Add-FederatedCredential "gh-env-$e" "repo:${Repo}:environment:$e" }

# --- RBAC (Contributor + User Access Administrator for role assignments) ---
$scope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"
foreach ($role in @('Contributor', 'User Access Administrator')) {
  Info "Assigning '$role' on $ResourceGroup..."
  az role assignment create --assignee-object-id $spObjectId --assignee-principal-type ServicePrincipal `
    --role "$role" --scope $scope 1>$null 2>$null
}

# --- GitHub environments --------------------------------------------------
foreach ($e in $Environments) {
  gh api -X PUT "repos/$Repo/environments/$e" 1>$null 2>$null
}

# --- GitHub secrets & variables -------------------------------------------
Info 'Setting GitHub Actions secrets and variables...'
function Set-Secret($n, $v) { $v | gh secret set $n --repo $Repo 1>$null }
Set-Secret 'AZURE_CLIENT_ID'          $appId
Set-Secret 'AZURE_TENANT_ID'          $tenantId
Set-Secret 'AZURE_SUBSCRIPTION_ID'    $SubscriptionId
Set-Secret 'DEPLOYER_OBJECT_ID'       $spObjectId
Set-Secret 'POSTGRES_ADMIN_PASSWORD'  $PostgresPassword
Set-Secret 'SEED_ADMIN_EMAIL'         $SeedAdminEmail
# Auth secrets start empty; scripts/setup-entra-auth fills them after the first deploy.
foreach ($n in @('ENTRA_CLIENT_ID','ENTRA_CLIENT_SECRET','GOOGLE_CLIENT_ID','GOOGLE_CLIENT_SECRET')) { Set-Secret $n '' }

gh variable set AZURE_RESOURCE_GROUP --repo $Repo --body $ResourceGroup 1>$null

Write-Host ''
Info 'Bootstrap complete.'
Write-Host @"
Saved to the local machine only (store safely):
  PostgreSQL admin password : $PostgresPassword

Next steps:
  1. Trigger the deploy pipeline:   gh workflow run deploy.yml --repo $Repo
     (or push to main). This provisions Azure and deploys the app.
  2. Configure authentication:      ./scripts/setup-entra-auth.ps1 -Repo $Repo -ResourceGroup $ResourceGroup
  3. Configure Google (manual):     see scripts/README.md
"@
