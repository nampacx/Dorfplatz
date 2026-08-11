#!/usr/bin/env bash
# Local end-to-end deploy without CI: Bicep provision + Function App publish + SWA upload.
# Usage: ./scripts/deploy.sh -s <subscriptionId> [-g bb-dev-rg] [-l westeurope] [-n dev] [-e you@example.com] [-p <pgpass>]
set -euo pipefail
RG="bb-dev-rg"; LOCATION="westeurope"; ENVN="dev"; SEED=""; PG=""
while getopts "s:g:l:n:e:p:" opt; do case $opt in
  s) SUB="$OPTARG";; g) RG="$OPTARG";; l) LOCATION="$OPTARG";; n) ENVN="$OPTARG";; e) SEED="$OPTARG";; p) PG="$OPTARG";; esac; done
: "${SUB:?-s subscriptionId required}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
info() { echo -e "\033[36m==> $*\033[0m"; }

az account show >/dev/null || { echo 'Run "az login" first.'; exit 1; }
az account set --subscription "$SUB"
DEPLOYER="$(az ad signed-in-user show --query id -o tsv)"
[ -n "$PG" ] || { PG="$(LC_ALL=C tr -dc 'A-Za-z0-9@#%' </dev/urandom | head -c 24)"; info "Generated PostgreSQL admin password: $PG (store this)"; }

info "Provisioning infrastructure..."
az group create -n "$RG" -l "$LOCATION" >/dev/null
OUT="$(az deployment group create -g "$RG" --name "bb-$ENVN-local" \
  --template-file "$ROOT/infra/main.bicep" \
  --parameters environmentName="$ENVN" postgresAdminPassword="$PG" deployerObjectId="$DEPLOYER" seedAdminEmail="$SEED" \
  --query properties.outputs -o json)"
FUNC="$(echo "$OUT" | jq -r .functionAppName.value)"
SWA="$(echo "$OUT" | jq -r .staticWebAppName.value)"
SWA_HOST="$(echo "$OUT" | jq -r .staticWebAppHostname.value)"
info "Function App: $FUNC   Static Web App: $SWA"

info "Publishing the Function App..."
PUB="$(mktemp -d)"; ZIP="$(mktemp -u).zip"
dotnet publish "$ROOT/api/BulletinBoard.Api.csproj" -c Release -o "$PUB" >/dev/null
( cd "$PUB" && zip -qr "$ZIP" . )
az functionapp deployment source config-zip -g "$RG" -n "$FUNC" --src "$ZIP" >/dev/null

info "Building and deploying the frontend..."
pushd "$ROOT/frontend" >/dev/null
[ -d node_modules ] || npm ci >/dev/null
VITE_API_BASE=/api npm run build >/dev/null
TOKEN="$(az staticwebapp secrets list --name "$SWA" --query 'properties.apiKey' -o tsv)"
npx --yes @azure/static-web-apps-cli deploy ./dist --deployment-token "$TOKEN" --env production >/dev/null
popd >/dev/null

echo
info "Done. Your app: https://$SWA_HOST"
echo "Next: ./scripts/setup-entra-auth.sh -r <owner/repo> -g $RG"
