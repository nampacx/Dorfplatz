<#
.SYNOPSIS
  Applies a Google OAuth client id/secret (created manually in Google Cloud Console)
  to the Static Web App settings and GitHub secrets.

.EXAMPLE
  ./scripts/set-google-auth.ps1 -Repo nampacx/BulletinBoard -ResourceGroup bb-dev-rg -ClientId <id> -ClientSecret <secret>
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $ClientId,
  [Parameter(Mandatory)] [string] $ClientSecret,
  [string] $Repo,                                        # auto-detected from the current checkout if omitted
  [string] $ResourceGroup = 'bb-dev-rg',
  [string] $SwaName
)

$ErrorActionPreference = 'Stop'
if (-not $Repo) {
  $Repo = gh repo view --json nameWithOwner -q .nameWithOwner 2>$null
  if (-not $Repo) { throw 'Could not detect the GitHub repo. Run inside the cloned repo or pass -Repo <owner>/<name>.' }
}
if (-not $SwaName) { $SwaName = az staticwebapp list -g $ResourceGroup --query '[0].name' -o tsv }

az staticwebapp appsettings set -n $SwaName -g $ResourceGroup `
  --setting-names "GOOGLE_CLIENT_ID=$ClientId" "GOOGLE_CLIENT_SECRET=$ClientSecret" 1>$null
$ClientId     | gh secret set GOOGLE_CLIENT_ID     --repo $Repo 1>$null
$ClientSecret | gh secret set GOOGLE_CLIENT_SECRET --repo $Repo 1>$null
Write-Host '==> Google sign-in configured.' -ForegroundColor Cyan
