#!/usr/bin/env bash
# Activate a Unity Personal license on THIS machine with your Unity ID.
# Run it yourself in a terminal (it prompts; nothing is stored or echoed):
#   ~/git/lookimportant/tools/activate-unity.sh
# A .ulf copied from another computer will not work: those files are bound to
# the machine that created them. Accounts that sign in with Google/Apple need a
# Unity ID password set first (id.unity.com > Account settings > Security).
set -euo pipefail
CLIENT="${UNITY_LICENSING_CLIENT:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Data/Resources/Licensing/Client/Unity.Licensing.Client}"

read -rp "Unity ID email: " UNITY_EMAIL
read -rsp "Unity ID password: " UNITY_PASSWORD
echo
"$CLIENT" --activate-all --include-personal --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"
unset UNITY_PASSWORD
echo
echo "Entitlements now granted on this machine:"
"$CLIENT" --showEntitlements | grep -E 'com\.unity\.editor|Personal|Expir' || true
