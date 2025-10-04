#!/usr/bin/env bash

# Create symbolic links from Broforce game directories to the Releases folder
# This is the Linux/NixOS equivalent of CREATE LINKS.bat

# Get the script's directory (Releases folder)
RELEASES_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Define game path
GAME_PATH="$HOME/.local/share/Steam/steamapps/common/Broforce"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "Creating links for all mods and bros..."
echo ""

# Check if game directory exists
if [ ! -d "$GAME_PATH" ]; then
    echo -e "${RED}Error: Broforce installation not found at $GAME_PATH${NC}"
    echo "Please ensure Broforce is installed via Steam"
    exit 1
fi

# Create directories if they don't exist
mkdir -p "$GAME_PATH/Mods"
mkdir -p "$GAME_PATH/BroMaker_Storage"

# Loop through all subdirectories in the Releases folder
for folder_path in "$RELEASES_DIR"/*/; do
    # Skip if not a directory
    [ -d "$folder_path" ] || continue

    folder_name=$(basename "$folder_path")

    # Skip if the inner folder doesn't exist (structure is Releases/ProjectName/ProjectName/)
    if [ ! -d "$folder_path/$folder_name" ]; then
        continue
    fi

    # Check if it's a bro (has .mod.json file)
    is_bro=0
    is_mod=0

    # Check for .mod.json files (bros)
    if ls "$folder_path/$folder_name"/*.mod.json >/dev/null 2>&1; then
        is_bro=1
    fi

    # Check for Info.json file (mods)
    if [ -f "$folder_path/$folder_name/Info.json" ]; then
        is_mod=1
    fi

    # Create appropriate link based on type
    if [ $is_bro -eq 1 ]; then
        target_dir="$GAME_PATH/BroMaker_Storage/$folder_name"

        # Check if path exists
        if [ -e "$target_dir" ]; then
            # Check if it's a symlink
            if [ -L "$target_dir" ]; then
                # Remove old symlink and recreate
                rm "$target_dir"
                echo "Updating bro link: $folder_name"
                ln -s "$folder_path/$folder_name" "$target_dir"
                echo -e "${GREEN}✓${NC} Updated link for bro: $folder_name"
            else
                echo -e "${YELLOW}WARNING${NC}: Non-symlink folder already exists for bro: $folder_name"
                echo "         Please remove or rename the existing folder at: $target_dir"
            fi
        else
            echo "Creating bro link: $folder_name"
            ln -s "$folder_path/$folder_name" "$target_dir"
            if [ $? -eq 0 ]; then
                echo -e "${GREEN}✓${NC} Created link for bro: $folder_name"
            else
                echo -e "${RED}WARNING${NC}: Failed to create link for bro: $folder_name"
            fi
        fi

    elif [ $is_mod -eq 1 ]; then
        target_dir="$GAME_PATH/Mods/$folder_name"

        # Check if path exists
        if [ -e "$target_dir" ]; then
            # Check if it's a symlink
            if [ -L "$target_dir" ]; then
                # Remove old symlink and recreate
                rm "$target_dir"
                echo "Updating mod link: $folder_name"
                ln -s "$folder_path/$folder_name" "$target_dir"
                echo -e "${GREEN}✓${NC} Updated link for mod: $folder_name"
            else
                echo -e "${YELLOW}WARNING${NC}: Non-symlink folder already exists for mod: $folder_name"
                echo "         Please remove or rename the existing folder at: $target_dir"
            fi
        else
            echo "Creating mod link: $folder_name"
            ln -s "$folder_path/$folder_name" "$target_dir"
            if [ $? -eq 0 ]; then
                echo -e "${GREEN}✓${NC} Created link for mod: $folder_name"
            else
                echo -e "${RED}WARNING${NC}: Failed to create link for mod: $folder_name"
            fi
        fi
    else
        echo -e "${YELLOW}Skipping${NC}: $folder_name (not identified as mod or bro)"
    fi
done

echo ""
echo "Link creation complete."
echo ""
echo "Symlinks created in:"
echo "  Bros: $GAME_PATH/BroMaker_Storage/"
echo "  Mods: $GAME_PATH/Mods/"