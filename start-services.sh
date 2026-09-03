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

if ! command -v npm >/dev/null 2>&1; then
    printf '%s\n' 'Fehler: npm wurde nicht gefunden. Für den Frontend-Dev-Server wird Node.js benötigt.' >&2
    exit 1
fi

if [[ ! -x frontend/node_modules/.bin/vite ]]; then
    printf '%s\n' 'Frontend-Abhängigkeiten fehlen. Installiere sie mit npm ci ...'
    (cd frontend && npm ci)
fi

cleanup() {
    trap - EXIT INT TERM
    if [[ -n "${frontend_pid:-}" ]]; then
        kill "$frontend_pid" 2>/dev/null || true
        wait "$frontend_pid" 2>/dev/null || true
    fi
    docker compose stop >/dev/null 2>&1 || true
}

trap cleanup EXIT INT TERM

docker compose up --build --detach "$@"

(cd frontend && npm run dev -- --host 0.0.0.0) &
frontend_pid=$!
wait "$frontend_pid"