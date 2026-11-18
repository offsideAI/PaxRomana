#!/bin/bash
# Collects what the Xbox knows about a sideloaded app that will not start, through the Xbox Device Portal.
# It only reads: nothing is installed, removed or changed (unless you pass --launch, which starts the app).
#
#   XBOX_IP=192.168.1.50 XBOX_USER=me XBOX_PASS=secret ./scripts/xbox-diagnose.sh [--launch] [package-name]
#
# package-name defaults to PaxRomana.TertiaRoma.Native (the Identity Name in Package.appxmanifest).
# --launch asks the console to start the app first, waits a few seconds, then collects.
#
# It prints: the console's OS version, how many users are signed in, the installed package, whether the app's process is
# running, crash dumps and error reports for it, the files in its local data folder, and its startup.log (the diary
# the game writes while it starts). Paste the whole output back when asking for help. The password is never printed.
set -u

: "${XBOX_IP:?Set XBOX_IP to the console address shown in Dev Home}"
: "${XBOX_USER:?Set XBOX_USER to the Device Portal user name}"
: "${XBOX_PASS:?Set XBOX_PASS to the Device Portal password}"

LAUNCH=0
NAME="PaxRomana.TertiaRoma.Native"
for arg in "$@"; do
  if [ "$arg" = "--launch" ]; then LAUNCH=1; else NAME="$arg"; fi
done

PORTAL="https://$XBOX_IP:11443"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
# The "auto-" user name prefix tells Device Portal this is a script. -k: the console's certificate is self-signed.
AUTH=(-k -sS -m 30 -u "auto-$XBOX_USER:$XBOX_PASS")

# get <label> <path-and-query> : fetches into $TMP/<label>, prints the HTTP status, returns 0 on 200
get() {
  local label="$1" url="$2" code
  code="$(curl "${AUTH[@]}" -o "$TMP/$label" -w '%{http_code}' "$PORTAL$url" 2>"$TMP/$label.err")" || code="curl-failed"
  echo "[$code] GET ${url%%\?*}"
  [ -s "$TMP/$label.err" ] && sed 's/^/    /' "$TMP/$label.err"
  [ "$code" = "200" ]
}
show() { head -c "${2:-3000}" "$TMP/$1" 2>/dev/null; echo; }
section() { echo; echo "=== $1 ==="; }

echo "Xbox diagnosis for package '$NAME' at $PORTAL, $(date -u '+%Y-%m-%d %H:%M:%S UTC')"

section "Console"
get os /api/os/info && show os 600
if get users /ext/user; then
  # only whether someone is signed in: account names and e-mail addresses are left out of the output
  python3 - "$TMP/users" <<'PY'
import json, sys
try:
    users = json.load(open(sys.argv[1], encoding="utf-8-sig")).get("Users", [])
    print("%d user(s) on the console, %d signed in" % (len(users), sum(1 for u in users if u.get("SignedIn"))))
except Exception as e:
    print("could not read the user list:", e)
PY
fi

section "Installed package"
FULL=""; PRAID=""
if get packages /api/app/packagemanager/packages; then
  python3 - "$TMP/packages" "$NAME" > "$TMP/pkg.txt" <<'PY'
import json, sys
try:
    data = json.load(open(sys.argv[1], encoding="utf-8-sig"))
except Exception as e:
    print("could not read the package list:", e); sys.exit()
want = sys.argv[2].lower()
found = [p for p in data.get("InstalledPackages", []) if want in (p.get("Name", "") + " " + p.get("PackageFullName", "")).lower()]
if not found:
    print("NOT INSTALLED: no package matching", sys.argv[2])
    print("sideloaded packages on the console:")
    for p in data.get("InstalledPackages", []):
        if p.get("PackageOrigin") in (5, "5") or "Dev" in str(p.get("PackageOrigin", "")):
            print("   ", p.get("PackageFullName"))
for p in found:
    print("FULL=" + p.get("PackageFullName", ""))
    print("PRAID=" + p.get("PackageRelativeId", ""))
    for k in ("Name", "PackageFullName", "PackageFamilyName", "PackageRelativeId", "Version", "PackageOrigin", "Publisher", "IsXAP", "CanUninstall"):
        if k in p:
            print("   %-18s %s" % (k, p[k]))
PY
  grep -v -E '^(FULL|PRAID)=' "$TMP/pkg.txt"
  FULL="$(grep '^FULL=' "$TMP/pkg.txt" | head -n 1 | cut -d= -f2-)"
  PRAID="$(grep '^PRAID=' "$TMP/pkg.txt" | head -n 1 | cut -d= -f2-)"
else
  show packages 600
fi

if [ "$LAUNCH" = "1" ]; then
  section "Launch (requested with --launch)"
  if [ -n "$FULL" ] && [ -n "$PRAID" ]; then
    B64_ID="$(printf '%s' "$PRAID" | base64 | tr -d '\n')"; B64_PKG="$(printf '%s' "$FULL" | base64 | tr -d '\n')"
    Q="appid=$(python3 -c 'import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1],safe=""))' "$B64_ID")&package=$(python3 -c 'import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1],safe=""))' "$B64_PKG")"
    code="$(curl "${AUTH[@]}" -o "$TMP/launch" -w '%{http_code}' -X POST "$PORTAL/api/taskmanager/app?$Q" 2>"$TMP/launch.err")" || code="curl-failed"
    echo "[$code] POST /api/taskmanager/app"; show launch 800
    echo "waiting 8 seconds for the app to start (or fail)..."; sleep 8
  else
    echo "skipped: the package was not found"
  fi
fi

section "Is it running?"
if get procs /api/resourcemanager/processes; then
  python3 - "$TMP/procs" "$NAME" <<'PY'
import json, sys
try:
    data = json.load(open(sys.argv[1], encoding="utf-8-sig"))
except Exception as e:
    print("could not read the process list:", e); sys.exit()
want = sys.argv[2].lower(); short = want.split(".")[0]
hits = [p for p in data.get("Processes", []) if want in json.dumps(p).lower() or "paxromananative" in json.dumps(p).lower()]
print("%d processes on the console; %d belong to this app" % (len(data.get("Processes", [])), len(hits)))
for p in hits:
    print("   ", {k: p.get(k) for k in ("ImageName", "ProcessId", "PackageFullName", "WorkingSetSize", "IsRunning") if k in p})
PY
else
  show procs 400
fi

section "Crash dumps and error reports"
if get dumps /api/debug/dump/usermode/dumps; then
  python3 - "$TMP/dumps" <<'PY'
import json, sys
try:
    data = json.load(open(sys.argv[1], encoding="utf-8-sig"))
    dumps = data.get("CrashDumps", [])
    print("%d crash dumps on the console" % len(dumps))
    for d in dumps[-15:]:
        print("   ", d)
except Exception as e:
    print(open(sys.argv[1], errors="replace").read()[:800])
PY
else
  show dumps 400
fi
if [ -n "$FULL" ]; then
  ENC_FULL="$(python3 -c 'import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1],safe=""))' "$FULL")"
  get crashcontrol "/api/debug/dump/usermode/crashcontrol?packageFullName=$ENC_FULL" && show crashcontrol 300
fi
get wer /api/wer/reports && show wer 2500

section "The app's local data folder and startup.log"
if [ -n "$FULL" ]; then
  GOT=0
  for P in '%5C%5CLocalState' '%5CLocalState' 'LocalState'; do
    if get files "/api/filesystem/apps/files?knownfolderid=LocalAppData&packagefullname=$ENC_FULL&path=$P"; then
      python3 - "$TMP/files" <<'PY'
import json, sys
try:
    for f in json.load(open(sys.argv[1], encoding="utf-8-sig")).get("Items", []):
        print("    %-28s %10s bytes" % (f.get("Name"), f.get("FileSize")))
except Exception as e:
    print(open(sys.argv[1], errors="replace").read()[:600])
PY
      if get log "/api/filesystem/apps/file?knownfolderid=LocalAppData&filename=startup.log&packagefullname=$ENC_FULL&path=$P"; then
        GOT=1
        echo "----- startup.log -----"
        cat "$TMP/log"
        echo "----- end of startup.log -----"
      fi
      break
    fi
  done
  if [ "$GOT" = "0" ]; then
    echo "No startup.log. Either this build is older than the one that writes it, or the app never got as far as"
    echo "running its own code (the log is created by the first line of the program)."
    show log 400
  fi
else
  echo "skipped: the package was not found"
fi

echo
echo "Done. Paste everything above."
