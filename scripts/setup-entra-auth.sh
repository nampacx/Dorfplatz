#!/usr/bin/env bash
# Creates/updates the Entra app registration for USER sign-in, points its redirect at
# the deployed SWA, generates a secret, and applies it to SWA settings + GitHub secrets.
# Run AFTER the first deploy (so the SWA hostname exists).
#
# Usage: ./scripts/setup-entra-auth.sh -r owner/repo [-g bb-dev-rg] [-n <swaName>]
set -euo pipefail
RG="bb-dev-rg"; APP_NAME="BulletinBoard-Auth"; SWA=""
while getopts "r:g:n:" opt; do case $opt in r) REPO="$OPTARG";; g) RG="$OPTARG";; n) SWA="$OPTARG";; esac; done
: "${REPO:?-r owner/repo required}"
info() { echo -e "\033[36m==> $*\033[0m"; }

az account show >/dev/null || { echo 'Run "az login" first.'; exit 1; }
[ -n "$SWA" ] || SWA="$(az staticwebapp list -g "$RG" --query '[0].name' -o tsv)"
[ -n "$SWA" ] || { echo "No Static Web App in $RG. Deploy infra first."; exit 1; }
HOST="$(az staticwebapp show -g "$RG" -n "$SWA" --query defaultHostname -o tsv)"
REDIRECT="https://$HOST/.auth/login/aad/callback"
info "SWA: $SWA ($HOST)"

APP_ID="$(az ad app list --display-name "$APP_NAME" --query '[0].appId' -o tsv)"
if [ -z "$APP_ID" ]; then
  info "Creating app registration '$APP_NAME'..."
  APP_ID="$(az ad app create --display-name "$APP_NAME" \
    --sign-in-audience AzureADandPersonalMicrosoftAccount \
    --web-redirect-uris "$REDIRECT" --query appId -o tsv)"
else
  az ad app update --id "$APP_ID" --web-redirect-uris "$REDIRECT" >/dev/null
fi

info "Creating a client secret..."
SECRET="$(az ad app credential reset --id "$APP_ID" --append --display-name swa --query password -o tsv)"

info "Applying settings to SWA + GitHub..."
az staticwebapp appsettings set -n "$SWA" -g "$RG" \
  --setting-names "ENTRA_CLIENT_ID=$APP_ID" "ENTRA_CLIENT_SECRET=$SECRET" >/dev/null
printf '%s' "$APP_ID" | gh secret set ENTRA_CLIENT_ID --repo "$REPO" >/dev/null
printf '%s' "$SECRET" | gh secret set ENTRA_CLIENT_SECRET --repo "$REPO" >/dev/null

echo
info "Microsoft sign-in configured."
cat <<EOF
Google (manual):
  1. Google Cloud Console -> Credentials -> OAuth client ID (Web).
  2. Redirect URI: https://$HOST/.auth/login/google/callback
  3. Then: ./scripts/set-google-auth.sh -r $REPO -g $RG -i <clientId> -x <clientSecret>
EOF
