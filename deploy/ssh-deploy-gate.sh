#!/usr/bin/env bash
# ssh-deploy-gate — the ONLY thing the GitHub Actions deploy key can run on the host.
#
# Installed by hand (NOT updated by deploys, on purpose) as /usr/local/sbin/smart-hadith-deploy and
# bound to the key in /root/.ssh/authorized_keys:
#
#   command="/usr/local/sbin/smart-hadith-deploy",restrict ssh-ed25519 AAAA... smart-hadith-deploy-github-actions
#
# `restrict` turns off port/agent/X11 forwarding and the pty; `command=` ignores whatever the client asked to
# run and puts it in $SSH_ORIGINAL_COMMAND instead. So a leaked DEPLOY_SSH_KEY can only ask "deploy commit X",
# and X must be on origin/master and not older than what is live: it cannot run commands, deploy unmerged code,
# or put old code back.
#
# Flow: validate SHA -> take the lock -> fetch -> check X is on master, not older than HEAD, and has the
# deploy script -> check it out -> hand over to the versioned deploy/deploy-remote.sh of THAT commit,
# passing the commit that was live before.
set -euo pipefail

SHA="${SSH_ORIGINAL_COMMAND:-}"
if [[ ! "$SHA" =~ ^[0-9a-f]{40}$ ]]; then
  echo "smart-hadith-deploy: expected a full 40-character commit SHA, got '${SHA:0:60}'" >&2
  exit 2
fi

# One deploy at a time, even if GitHub's concurrency group is bypassed (e.g. a manual run on the host).
exec 9>/var/lock/smart-hadith-deploy.lock
flock -w 1800 9 || { echo "smart-hadith-deploy: another deploy has held the lock for 30 min" >&2; exit 3; }

cd /opt/smart-hadith-tree
git fetch --quiet origin master
if ! git merge-base --is-ancestor "$SHA" origin/master; then
  echo "smart-hadith-deploy: $SHA is not on origin/master, refusing" >&2
  exit 2
fi
# Forward only: the live commit or a newer one. Stops a stale queued run (or a leaked key) from putting old
# code back. Roll back with a revert PR, which deploys like any other merge.
BEFORE="$(git rev-parse HEAD)"
if ! git merge-base --is-ancestor "$BEFORE" "$SHA"; then
  echo "smart-hadith-deploy: $SHA is older than the live ${BEFORE:0:7}, refusing (roll back with a revert PR)" >&2
  exit 2
fi
# Check before touching the checkout, so a refused deploy leaves the tree exactly as it was.
if ! git cat-file -e "$SHA:deploy/deploy-remote.sh" 2>/dev/null; then
  echo "smart-hadith-deploy: $SHA has no deploy/deploy-remote.sh, refusing" >&2
  exit 2
fi

# Untracked host-only files (.env, compose.edge.yml, backups/*.dump and the old *.bak) survive a hard reset.
git reset --quiet --hard "$SHA"
exec ./deploy/deploy-remote.sh "$BEFORE"
