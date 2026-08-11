#!/usr/bin/env bash
# Deletes the resource group. Optionally purges the soft-deleted Key Vault.
# Usage: ./scripts/teardown.sh -s <subscriptionId> [-g bb-dev-rg] [-P]
set -euo pipefail
RG="bb-dev-rg"; PURGE=0
while getopts "s:g:P" opt; do case $opt in s) SUB="$OPTARG";; g) RG="$OPTARG";; P) PURGE=1;; esac; done
: "${SUB:?-s subscriptionId required}"
az account set --subscription "$SUB"

KV="$(az keyvault list -g "$RG" --query '[0].name' -o tsv 2>/dev/null || true)"
echo "Deleting resource group '$RG'..."
az group delete -n "$RG" --yes >/dev/null
if [ "$PURGE" = "1" ] && [ -n "$KV" ]; then
  echo "Purging soft-deleted Key Vault '$KV'..."
  az keyvault purge --name "$KV" >/dev/null 2>&1 || true
fi
echo -e "\033[36m==> Teardown complete.\033[0m"
