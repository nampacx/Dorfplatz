#!/usr/bin/env bash
# One-time bootstrap: GitHub -> Azure OIDC deploy identity, RBAC, resource group,
# and all GitHub Actions secrets/variables. After this, deploy.yml needs no manual wiring.
#
# Usage:
#   ./scripts/bootstrap-github-oidc.sh -s <subscriptionId> -r owner/BulletinBoard [-g bb-dev-rg] [-l westeurope] [-e you@example.com] [-f]
#   -f repairs federated credentials only; does not change secrets, RBAC, or resources.
#
# Requires: az (logged in, Owner/UAA on the sub) and gh (logged in).
set -euo pipefail

RG="bb-dev-rg"; LOCATION="westeurope"; APP_NAME="bb-github-deploy"; SEED_EMAIL=""; PG_PASS=""; REPO=""
FIC_ONLY=false
ENVS=("dev" "prod")
while getopts "s:r:g:l:e:p:f" opt; do
  case $opt in
    s) SUB="$OPTARG";; r) REPO="$OPTARG";; g) RG="$OPTARG";;
    l) LOCATION="$OPTARG";; e) SEED_EMAIL="$OPTARG";; p) PG_PASS="$OPTARG";;
    f) FIC_ONLY=true;;
    *) echo "invalid option"; exit 1;;
  esac
done
: "${SUB:?-s subscriptionId required}"
info() { echo -e "\033[36m==> $*\033[0m"; }

info "Checking prerequisites..."
az account show >/dev/null || { echo 'Run "az login" first.'; exit 1; }
gh auth status >/dev/null 2>&1 || { echo 'Run "gh auth login" first.'; exit 1; }

# Auto-detect the GitHub repo from the current checkout when not passed with -r.
if [ -z "$REPO" ]; then
  REPO="$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null || true)"
  [ -n "$REPO" ] || { echo 'Could not detect the GitHub repo. Run inside the cloned repo or pass -r owner/name.'; exit 1; }
  info "Detected repo: $REPO"
fi

SUBJECT_PREFIX="$(gh api -H 'X-GitHub-Api-Version: 2026-03-10' "repos/$REPO/actions/oidc/customization/sub" --jq \
  'if .use_default or .include_claim_keys == ["repo", "context"] then .sub_claim_prefix // empty else error("Unsupported custom OIDC subject template.") end')"
[[ "$SUBJECT_PREFIX" == repo:* ]] || { echo "Missing or unexpected OIDC subject prefix." >&2; exit 1; }
SUBJECT_PREFIX="${SUBJECT_PREFIX%:}"

if ! $FIC_ONLY; then
  [ -n "$PG_PASS" ] || { PG_PASS="$(LC_ALL=C tr -dc 'A-Za-z0-9@#%' </dev/urandom | head -c 24)"; info "Generated PostgreSQL admin password."; }
fi

az account set --subscription "$SUB"
TENANT="$(az account show --query tenantId -o tsv)"

if ! $FIC_ONLY; then
  info "Ensuring resource group '$RG'..."
  az group create -n "$RG" -l "$LOCATION" >/dev/null
fi

info "Creating app registration '$APP_NAME'..."
APP_ID="$(az ad app list --display-name "$APP_NAME" --query '[0].appId' -o tsv)"
if $FIC_ONLY && [ -z "$APP_ID" ]; then
  echo "Existing deployment app '$APP_NAME' not found." >&2
  exit 1
fi
[ -n "$APP_ID" ] || APP_ID="$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)"
info "Client id: $APP_ID"

add_fic() {
  local name="$1" subject="$2" credential_id payload
  credential_id="$(az ad app federated-credential list --id "$APP_ID" --query "[?name=='$name'] | [0].id" -o tsv)"
  payload="\"issuer\":\"https://token.actions.githubusercontent.com\",\"subject\":\"$subject\",\"audiences\":[\"api://AzureADTokenExchange\"]"
  if [ -n "$credential_id" ]; then
    az ad app federated-credential update --id "$APP_ID" --federated-credential-id "$credential_id" --parameters "{$payload}" >/dev/null
  else
    az ad app federated-credential create --id "$APP_ID" --parameters "{\"name\":\"$name\",$payload}" >/dev/null
  fi
  info "Federated credential: $subject"
}
add_fic "gh-main" "${SUBJECT_PREFIX}:ref:refs/heads/main"
add_fic "gh-pr"   "${SUBJECT_PREFIX}:pull_request"
for e in "${ENVS[@]}"; do add_fic "gh-env-$e" "${SUBJECT_PREFIX}:environment:$e"; done

if $FIC_ONLY; then
  info "Federated credentials repaired. GitHub secrets and Azure resources were not changed."
  exit 0
fi

SP_OID="$(az ad sp list --filter "appId eq '$APP_ID'" --query '[0].id' -o tsv)"
[ -n "$SP_OID" ] || SP_OID="$(az ad sp create --id "$APP_ID" --query id -o tsv)"

SCOPE="/subscriptions/$SUB/resourceGroups/$RG"
for role in "Contributor" "User Access Administrator"; do
  info "Assigning '$role'..."
  az role assignment create --assignee-object-id "$SP_OID" --assignee-principal-type ServicePrincipal \
    --role "$role" --scope "$SCOPE" >/dev/null 2>&1 || true
done

for e in "${ENVS[@]}"; do gh api -X PUT "repos/$REPO/environments/$e" >/dev/null 2>&1 || true; done

info "Setting GitHub secrets and variables..."
set_secret() { printf '%s' "$2" | gh secret set "$1" --repo "$REPO" >/dev/null; }
set_secret AZURE_CLIENT_ID "$APP_ID"
set_secret AZURE_TENANT_ID "$TENANT"
set_secret AZURE_SUBSCRIPTION_ID "$SUB"
set_secret DEPLOYER_OBJECT_ID "$SP_OID"
set_secret POSTGRES_ADMIN_PASSWORD "$PG_PASS"
set_secret SEED_ADMIN_EMAIL "$SEED_EMAIL"
for n in ENTRA_CLIENT_ID ENTRA_CLIENT_SECRET GOOGLE_CLIENT_ID GOOGLE_CLIENT_SECRET; do set_secret "$n" ""; done
gh variable set AZURE_RESOURCE_GROUP --repo "$REPO" --body "$RG" >/dev/null

echo
info "Bootstrap complete."
cat <<EOF
PostgreSQL admin password (store safely): $PG_PASS

Next:
  1. gh workflow run deploy.yml --repo $REPO      # provision + deploy
  2. ./scripts/setup-entra-auth.sh -r $REPO -g $RG # configure Microsoft sign-in
  3. Google: see scripts/README.md
EOF
