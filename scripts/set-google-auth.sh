#!/usr/bin/env bash
# Applies a Google OAuth client id/secret to SWA settings + GitHub secrets.
# Usage: ./scripts/set-google-auth.sh -r owner/repo -i <clientId> -x <clientSecret> [-g bb-dev-rg] [-n swaName]
set -euo pipefail
RG="bb-dev-rg"; SWA=""
while getopts "r:i:x:g:n:" opt; do case $opt in r) REPO="$OPTARG";; i) CID="$OPTARG";; x) CSEC="$OPTARG";; g) RG="$OPTARG";; n) SWA="$OPTARG";; esac; done
: "${REPO:?}"; : "${CID:?}"; : "${CSEC:?}"
[ -n "$SWA" ] || SWA="$(az staticwebapp list -g "$RG" --query '[0].name' -o tsv)"

az staticwebapp appsettings set -n "$SWA" -g "$RG" \
  --setting-names "GOOGLE_CLIENT_ID=$CID" "GOOGLE_CLIENT_SECRET=$CSEC" >/dev/null
printf '%s' "$CID"  | gh secret set GOOGLE_CLIENT_ID --repo "$REPO" >/dev/null
printf '%s' "$CSEC" | gh secret set GOOGLE_CLIENT_SECRET --repo "$REPO" >/dev/null
echo -e "\033[36m==> Google sign-in configured.\033[0m"
