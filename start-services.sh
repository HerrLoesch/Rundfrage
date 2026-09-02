#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

if ! command -v docker >/dev/null 2>&1; then
    printf '%s\n' 'Fehler: Docker wurde nicht gefunden.' >&2
    exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
    printf '%s\n' 'Fehler: Docker Compose ist nicht verfügbar.' >&2
    exit 1
fi

cd "$script_dir"
exec docker compose up --build "$@"