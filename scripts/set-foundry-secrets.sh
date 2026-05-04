#!/usr/bin/env bash
#
# Set Loom's Foundry user-secrets for local development.
#
# Wraps `dotnet user-secrets set` for the Loom.Web project, populating the
# Foundry:* configuration block with the team's current Azure OpenAI
# deployment in France Central. Re-running overwrites previous values.
#
# Usage:
#   scripts/set-foundry-secrets.sh                # prompts for the API key
#   FOUNDRY_API_KEY=... scripts/set-foundry-secrets.sh
#
# Override defaults via env vars:
#   FOUNDRY_ENDPOINT       (default https://artio-dev-fr-foundry.openai.azure.com)
#   FOUNDRY_DEPLOYMENT     (default marketplace-prompt)
#   FOUNDRY_API_VERSION    (default 2024-02-01)
#   FOUNDRY_DEFAULT_MODEL  (default gpt-5.4)

set -euo pipefail

ENDPOINT="${FOUNDRY_ENDPOINT:-https://artio-dev-fr-foundry.openai.azure.com}"
DEPLOYMENT="${FOUNDRY_DEPLOYMENT:-marketplace-prompt}"
API_VERSION="${FOUNDRY_API_VERSION:-2024-02-01}"
DEFAULT_MODEL="${FOUNDRY_DEFAULT_MODEL:-gpt-5.4}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo_root/src/Loom.Web/Loom.Web.csproj"

if [[ ! -f "$project" ]]; then
    echo "Loom.Web project not found at $project" >&2
    exit 1
fi

if [[ -z "${FOUNDRY_API_KEY:-}" ]]; then
    read -rs -p "Foundry API key: " FOUNDRY_API_KEY
    echo
fi

if [[ -z "$FOUNDRY_API_KEY" ]]; then
    echo "API key is required." >&2
    exit 1
fi

set_secret() {
    local key="$1"
    local value="$2"
    dotnet user-secrets set "$key" "$value" --project "$project" >/dev/null
}

echo "Setting Foundry user-secrets on Loom.Web..."

set_secret 'Foundry:Endpoint'     "$ENDPOINT"
set_secret 'Foundry:Deployment'   "$DEPLOYMENT"
set_secret 'Foundry:ApiVersion'   "$API_VERSION"
set_secret 'Foundry:DefaultModel' "$DEFAULT_MODEL"
set_secret 'Foundry:ApiKey'       "$FOUNDRY_API_KEY"

echo "Done. Configured $DEPLOYMENT on $ENDPOINT (api-version $API_VERSION)."
echo "Verify with: dotnet user-secrets list --project src/Loom.Web | grep '^Foundry:'"
