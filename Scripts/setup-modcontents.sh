#!/usr/bin/env bash

# Setup script to create symlinks for all _ModContent folders
# Creates ModContents/<ModName> -> <ModName>/<ModName>/_ModContent

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"
MODCONTENTS_DIR="$REPO_DIR/ModContents"

# Remove and recreate ModContents directory
rm -rf "$MODCONTENTS_DIR"
mkdir -p "$MODCONTENTS_DIR"

# Find all _ModContent directories and create symlinks
find "$REPO_DIR" -type d -name "_ModContent" | while read -r modcontent_path; do
    # Skip if it's inside ModContents (avoid recursion)
    if [[ "$modcontent_path" == *"/ModContents/"* ]]; then
        continue
    fi

    # Get the mod name (grandparent folder name)
    mod_name="$(basename "$(dirname "$(dirname "$modcontent_path")")")"

    # Skip if mod_name is the repo root
    if [[ "$mod_name" == "BroforceMods" ]]; then
        continue
    fi

    symlink_path="$MODCONTENTS_DIR/$mod_name"

    # Remove existing symlink if present
    if [[ -L "$symlink_path" ]]; then
        rm "$symlink_path"
    fi

    # Create symlink
    ln -s "$modcontent_path" "$symlink_path"
    echo "Created: ModContents/$mod_name -> $modcontent_path"
done

echo "Done!"
