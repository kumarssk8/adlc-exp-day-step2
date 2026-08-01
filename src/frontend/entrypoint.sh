#!/bin/sh
set -eu

INDEX_HTML="/usr/share/nginx/html/index.html"

if [ -f "$INDEX_HTML" ]; then
  API_URL="${VITE_API_URL:-}"
  # Escape `&` for sed replacement.
  ESCAPED_API_URL=$(printf '%s' "$API_URL" | sed 's/[&]/\\&/g')
  sed -i "s|__VITE_API_URL__|$ESCAPED_API_URL|g" "$INDEX_HTML"
fi

exec "$@"
