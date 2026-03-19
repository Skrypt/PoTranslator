#!/bin/bash
set -e

# Defaults
PROVIDER="${1:-google-service-account}"
LANG_CODE="${2:-fr}"
API_KEY="${3:-}"
MODEL="${4:-}"

# Paths
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SOURCE_PATH="$REPO_ROOT/src"
TARGET_PATH="$REPO_ROOT/tmp/translations"
HOST_PATH="$SOURCE_PATH/OrchardCore.Cms.Web/Localization"

# Clean and create temp directory
if [ -d "$TARGET_PATH" ]; then
    rm -rf "$(dirname "$TARGET_PATH")"
fi
mkdir -p "$TARGET_PATH"

# Install and run PO extractor
dotnet tool uninstall --global OrchardCoreContrib.PoExtractor 2>/dev/null || true
dotnet tool install --global OrchardCoreContrib.PoExtractor
extractpo "$SOURCE_PATH" "$TARGET_PATH"

# Build the translator
dotnet build "$SCRIPT_DIR" --configuration Release --nologo -v q

# Set credentials for Google service account provider
if [ "$PROVIDER" = "google-service-account" ]; then
    export GOOGLE_APPLICATION_CREDENTIALS="$SCRIPT_DIR/google-credentials.json"
fi

# Build arguments
TRANSLATOR_ARGS=(
    "--provider" "$PROVIDER"
    "--lang" "$LANG_CODE"
    "--po-source" "$TARGET_PATH"
    "--po-dest" "$HOST_PATH/$LANG_CODE"
)

if [ -n "$API_KEY" ]; then
    TRANSLATOR_ARGS+=("--api-key" "$API_KEY")
fi

if [ -n "$MODEL" ]; then
    TRANSLATOR_ARGS+=("--model" "$MODEL")
fi

# Run the translator
dotnet run --project "$SCRIPT_DIR" --configuration Release --no-build -- "${TRANSLATOR_ARGS[@]}"

# Clean up
rm -rf "$(dirname "$TARGET_PATH")"

echo "Translation completed successfully!"
