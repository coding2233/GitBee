#!/usr/bin/env bash
# GitBee Quick Script — example-status
# Shows git status summary for the current repository.
# The repo path is passed as $1 by GitBee.
#
# Install: place this file in ~/.config/GitBee/scripts/ (Linux/Mac)
#          or %APPDATA%/GitBee/scripts/ (Windows)
#          and it will appear in the Scripts panel.

REPO_PATH="$1"
if [ -z "$REPO_PATH" ]; then
    echo "Usage: $0 <repo-path>"
    exit 1
fi

cd "$REPO_PATH" || { echo "Error: cannot access $REPO_PATH"; exit 1; }

echo "=== Repository: $(basename "$REPO_PATH") ==="
echo ""

# Current branch
BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
echo "Branch: $BRANCH"

# Status
echo ""
echo "--- Status ---"
git status --short 2>/dev/null || echo "(clean)"

# Recent commits
echo ""
echo "--- Recent Commits ---"
git log --oneline --abbrev=8 -5 2>/dev/null || echo "(no commits)"

# Remote info
echo ""
REMOTE=$(git remote -v 2>/dev/null | head -2)
if [ -n "$REMOTE" ]; then
    echo "--- Remotes ---"
    echo "$REMOTE"
fi
