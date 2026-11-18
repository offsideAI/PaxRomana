#!/bin/bash
# Installs the WSBX package on an Xbox in Developer Mode through the Xbox Device Portal.
#
#   XBOX_IP=192.168.1.50 XBOX_USER=me XBOX_PASS=secret ./deploy.sh [package-dir]
#
# package-dir defaults to ./package. If it is missing, the newest build is downloaded from GitHub Actions with `gh`.
# Delete ./package to fetch a newer build.
set -euo pipefail
cd "$(dirname "$0")"

: "${XBOX_IP:?Set XBOX_IP to the console address shown in Dev Home}"
: "${XBOX_USER:?Set XBOX_USER to the Device Portal user name}"
: "${XBOX_PASS:?Set XBOX_PASS to the Device Portal password}"
DIR="${1:-package}"
PORTAL="https://$XBOX_IP:11443"

if [ ! -d "$DIR" ]; then
  echo "Downloading the newest build from GitHub Actions..."
  gh run download --name wsbx-xbox-package --dir "$DIR"
fi

MAIN="$(find "$DIR" -type f \( -name 'WSBX_*.appx' -o -name 'WSBX_*.msix' -o -name 'WSBX_*.appxbundle' -o -name 'WSBX_*.msixbundle' \) | head -n 1)"
[ -n "$MAIN" ] || { echo "No WSBX package found in $DIR" >&2; exit 1; }
echo "Package: $MAIN"

FORM=(-F "$(basename "$MAIN")=@$MAIN")
while IFS= read -r dep; do
  echo "Dependency: $dep"
  FORM+=(-F "$(basename "$dep")=@$dep")
done < <(find "$DIR" -type f -path '*Dependencies/x64/*' \( -name '*.appx' -o -name '*.msix' \))

# The "auto-" user name prefix tells Device Portal this is a script, so it does not ask for a CSRF token.
# -k: the console's certificate is self-signed.
AUTH=(-k -sS -u "auto-$XBOX_USER:$XBOX_PASS")
STATE="$(mktemp)"; trap 'rm -f "$STATE"' EXIT
echo "Uploading to $PORTAL ..."
curl "${AUTH[@]}" --fail-with-body -X POST "$PORTAL/api/app/packagemanager/package?package=$(basename "$MAIN")" "${FORM[@]}"
echo

for _ in $(seq 1 60); do
  sleep 2
  code="$(curl "${AUTH[@]}" -o "$STATE" -w '%{http_code}' "$PORTAL/api/app/packagemanager/state")"
  if [ "$code" = "200" ]; then
    cat "$STATE"; echo
    echo "Install finished. Check the result above, then launch WSBX from Dev Home on the console."
    exit 0
  fi
  echo "Installing... (HTTP $code)"
done
echo "Timed out waiting for the install to finish; check Device Portal in a browser." >&2
exit 1
