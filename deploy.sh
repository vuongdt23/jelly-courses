#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
CONTAINER="jellyfin-courses-dev"

echo "Building and deploying plugin..."
dotnet build "$PROJECT_DIR/Jellyfin.Plugin.Courses" --nologo -v quiet

echo "Restarting Jellyfin..."
docker restart "$CONTAINER"

echo "Done — http://localhost:8096"
