#!/bin/bash

# Create a new git worktree with proper path fixes for Windows compatibility
# and set up CLAUDE.md symlinks automatically

set -e

# Check if we're in the BroforceMods directory
if [[ ! -f "Repository.json" ]] || [[ ! -d ".git" ]]; then
    echo "Error: This script must be run from the BroforceMods root directory"
    exit 1
fi

# Check arguments
if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <worktree-name> [branch-name]"
    echo "Example: $0 ../BroforceMods-feature feature/new-feature"
    echo "Example: $0 ../BroforceMods-session2"
    exit 1
fi

WORKTREE_PATH="$1"
BRANCH_NAME="${2:-}"

# Get absolute paths
MAIN_REPO_PATH=$(pwd)
WORKTREE_NAME=$(basename "$WORKTREE_PATH")

echo "Creating worktree: $WORKTREE_PATH"

# Create the worktree
if [[ -n "$BRANCH_NAME" ]]; then
    git worktree add "$WORKTREE_PATH" -b "$BRANCH_NAME"
else
    git worktree add "$WORKTREE_PATH"
fi

echo "Fixing git paths for Windows compatibility..."

# Fix the .git file in the worktree to use relative path
WORKTREE_GIT_FILE="$WORKTREE_PATH/.git"
if [[ -f "$WORKTREE_GIT_FILE" ]]; then
    # Calculate relative path from worktree to main repo
    REL_PATH="../$(basename "$MAIN_REPO_PATH")"
    sed -i "s|^gitdir: .*|gitdir: $REL_PATH/.git/worktrees/$WORKTREE_NAME|" "$WORKTREE_GIT_FILE"
    echo "Updated $WORKTREE_GIT_FILE to use relative path"
fi

# Note: We intentionally don't modify the gitdir file in the main repo
# because it needs absolute paths for git worktree remove to work properly

# Set up CLAUDE.md symlinks if the claude worktree exists
CLAUDE_WORKTREE="../BroforceMods-claude"
if [[ -d "$CLAUDE_WORKTREE" ]]; then
    echo "Setting up CLAUDE.md symlinks..."
    
    # Convert paths to Windows format for the bat script
    CLAUDE_WIN_PATH=$(realpath "$CLAUDE_WORKTREE" | sed 's|/mnt/\([a-z]\)/|\1:/|' | sed 's|/|\\|g')
    WORKTREE_WIN_PATH=$(realpath "$WORKTREE_PATH" | sed 's|/mnt/\([a-z]\)/|\1:/|' | sed 's|/|\\|g')
    
    # Run the setup-symlinks.bat script with the worktree as target
    if [[ -f "$CLAUDE_WORKTREE/setup-symlinks.bat" ]]; then
        echo "Running setup-symlinks.bat for $WORKTREE_NAME (will prompt for admin)..."
        # Use PowerShell to run the batch file as admin
        powershell.exe -Command "Start-Process cmd -ArgumentList '/c cd /d \"$CLAUDE_WIN_PATH\" && setup-symlinks.bat \"$WORKTREE_WIN_PATH\" && pause' -Verb RunAs"
    else
        echo "Warning: setup-symlinks.bat not found in $CLAUDE_WORKTREE"
    fi
else
    echo "Warning: CLAUDE worktree not found at $CLAUDE_WORKTREE"
    echo "Skipping CLAUDE.md symlink setup"
fi

echo ""
echo "Worktree created successfully at: $WORKTREE_PATH"
echo "All paths have been fixed for Windows compatibility."
echo ""
echo "To use the worktree:"
echo "  cd $WORKTREE_PATH"
echo "  git status"