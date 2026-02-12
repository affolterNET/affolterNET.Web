#!/bin/bash
# Script to upload a NuGet API key to GitHub as a secret
# Usage: ./upload-nuget-key.sh <your-nuget-api-key>

set -e

SECRET_NAME="NUGET_API_KEY"
REPO="affolterNET/affolterNET.Web"

if ! command -v gh &> /dev/null; then
    echo "GitHub CLI (gh) not found. Please install it: https://cli.github.com/"
    exit 1
fi

if [ -z "$1" ]; then
    echo "Usage: $0 <your-nuget-api-key>"
    exit 1
fi

API_KEY="$1"

echo "Uploading NuGet API key to GitHub repository $REPO as secret $SECRET_NAME..."
gh secret set "$SECRET_NAME" -b"$API_KEY" -R "$REPO"

echo "✅ Secret $SECRET_NAME uploaded to $REPO."
