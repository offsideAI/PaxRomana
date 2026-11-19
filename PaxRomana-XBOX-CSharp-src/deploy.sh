#!/bin/bash
# Installs the native C# edition of Pax Romana: Tertia Roma on an Xbox in Developer Mode through the Xbox Device Portal.
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
  gh run download --name paxromana-native-xbox-package --dir "$DIR"
fi

MAIN="$(find "$DIR" -type f \( -name 'PaxRomanaNative_*.appx' -o -name 'PaxRomanaNative_*.msix' -o -name 'PaxRomanaNative_*.appxbundle' -o -name 'PaxRomanaNative_*.msixbundle' \) | head -n 1)"
[ -n "$MAIN" ] || { echo "No PaxRomanaNative package found in $DIR" >&2; exit 1; }
echo "Package: $MAIN"
# the version is part of the file name: PaxRomanaNative_<version>_x64.msix
WANT="$(basename "$MAIN" | sed -E 's/^PaxRomanaNative_([0-9.]+)_.*/\1/')"
echo "Version: $WANT"

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
    # The console can report success and still keep an older build, so ask it which version it now has.
    HAVE="$(curl "${AUTH[@]}" "$PORTAL/api/app/packagemanager/packages" | python3 -c '
import json, sys
try:
    for p in json.loads(sys.stdin.read().lstrip("\ufeff")).get("InstalledPackages", []):
        if "PaxRomana.TertiaRoma.Native" in p.get("PackageFullName", ""):
            v = p.get("Version", {})
            print("%s.%s.%s.%s" % (v.get("Major"), v.get("Minor"), v.get("Build"), v.get("Revision")))
            break
except Exception:
    pass
' 2>/dev/null || true)"
    if [ "$HAVE" = "$WANT" ]; then
      echo "Installed: version $HAVE is on the console. Launch 'Pax Romana: Tertia Roma (Native)' from Dev Home."
      exit 0
    fi
    echo "NOT UPDATED: this package is version $WANT but the console has '${HAVE:-none}'." >&2
    echo "Uninstall the app in Dev Home (highlight it, press the Menu button, Uninstall) and run this script again." >&2
    exit 1
  fi
  echo "Installing... (HTTP $code)"
done
echo "Timed out waiting for the install to finish; check Device Portal in a browser." >&2
exit 1
