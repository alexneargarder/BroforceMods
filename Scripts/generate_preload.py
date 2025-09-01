#!/usr/bin/env python3
"""
Generate PreloadAssets code by scanning a bro's Sprites and Sounds folders.
Usage: python generate_preload.py "Bro Name"

Requires BROFORCEPATH environment variable to be set, or falls back to default location.
"""

import os
import sys
from pathlib import Path
import subprocess
import json

def copy_to_clipboard(text):
    """Copy text to clipboard using platform-appropriate method."""
    try:
        # Try using xclip for WSL/Linux
        subprocess.run(['clip.exe'], input=text.encode(), check=True)
        return True
    except:
        try:
            # Try Windows clip command
            subprocess.run(['clip'], input=text.encode(), check=True)
            return True
        except:
            try:
                # Try xclip for Linux
                subprocess.run(['xclip', '-selection', 'clipboard'], input=text.encode(), check=True)
                return True
            except:
                return False

def extract_assets_from_json(json_data):
    """Recursively extract all .png and .wav filenames from JSON data."""
    assets = set()
    
    def extract_recursive(obj):
        if isinstance(obj, str):
            # Check if it's an asset file
            if obj.endswith('.png') or obj.endswith('.wav'):
                assets.add(obj)
        elif isinstance(obj, list):
            for item in obj:
                extract_recursive(item)
        elif isinstance(obj, dict):
            for value in obj.values():
                extract_recursive(value)
    
    extract_recursive(json_data)
    return assets

def find_assets(bro_name):
    """Find all sprites and sounds for a given bro, including nested folders."""
    # Get Broforce path from environment variable or use default
    broforce_path = os.environ.get('BROFORCEPATH')
    if not broforce_path:
        # Default fallback for common installation
        if os.path.exists("/mnt/c/Program Files (x86)/Steam/steamapps/common/Broforce"):
            broforce_path = "/mnt/c/Program Files (x86)/Steam/steamapps/common/Broforce"
        elif os.path.exists("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Broforce"):
            broforce_path = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Broforce"
    
    # Convert Windows path to WSL path if needed
    if broforce_path and broforce_path[1:3] == ':\\':
        drive = broforce_path[0].lower()
        broforce_path = f"/mnt/{drive}/{broforce_path[3:].replace(chr(92), '/')}"
    
    # Check the Releases folder and BroMaker_Storage (which are symlinked)
    # Handle both spaced and non-spaced naming conventions
    bro_no_space = bro_name.replace(' ', '')
    possible_paths = []
    
    # Try to detect the repo root
    script_dir = Path(__file__).parent
    repo_root = script_dir.parent  # Assuming script is in Scripts folder
    
    # Primary location: Releases folder (contains the actual assets)
    # Note: Releases folder has nested structure: Releases/[Bro Name]/[Bro Name]/
    possible_paths.append(str(repo_root / "Releases" / bro_name / bro_name))
    
    # Secondary location: BroMaker_Storage (if BROFORCEPATH is set)
    if broforce_path:
        possible_paths.append(f"{broforce_path}/BroMaker_Storage/{bro_no_space}")
    
    base_path = None
    for path in possible_paths:
        if os.path.exists(path):
            base_path = Path(path)
            break
    
    if not base_path:
        print(f"Could not find assets for '{bro_name}' in any of these locations:")
        for path in possible_paths:
            print(f"  - {path}")
        if not broforce_path:
            print("\nNote: BROFORCEPATH environment variable is not set.")
            print("Set it to your Broforce installation path for better detection.")
        return None
    
    # Load the JSON file to find auto-loaded assets
    json_assets = set()
    json_path = base_path / f"{bro_name}.json"
    if not json_path.exists():
        # Try without spaces in the name
        json_path = base_path / f"{bro_name.replace(' ', '')}.json"
    
    if json_path.exists():
        try:
            with open(json_path, 'r') as f:
                json_data = json.load(f)
                json_assets = extract_assets_from_json(json_data)
                print(f"Found {len(json_assets)} assets in JSON that will be auto-loaded by BroMaker")
        except Exception as e:
            print(f"Warning: Could not parse JSON file: {e}")
    else:
        print(f"Warning: Could not find JSON file at {json_path}")
    
    # Structure to hold all assets
    assets = {
        'sprites': [],
        'projectile_sprites': [],
        'sounds': {}  # Will be dict with subfolder as key
    }
    
    # In Releases folder, sprites are in the root, projectiles in "projectiles" folder, sounds in "sounds" folder
    
    # Find regular sprites (PNG files in root directory)
    for file in sorted(base_path.glob("*.png")):
        filename = file.name
        # Skip if it's in the JSON (auto-loaded by BroMaker)
        if filename in json_assets:
            continue
        # Skip certain files that aren't sprites to load
        if any(skip in filename.lower() for skip in ['avatar', 'cutscene', 'special']):
            continue
        assets['sprites'].append(filename)
    
    # Find projectile sprites
    projectiles_dir = base_path / "projectiles"
    if projectiles_dir.exists():
        for file in sorted(projectiles_dir.glob("*.png")):
            # Skip if it's in the JSON (auto-loaded by BroMaker)
            if file.name not in json_assets:
                assets['projectile_sprites'].append(file.name)
    
    # Find sounds (including nested folders)
    sounds_dir = base_path / "sounds"
    if sounds_dir.exists():
        # First get root level sounds
        root_sounds = []
        for file in sorted(sounds_dir.glob("*.wav")):
            # Skip if it's in the JSON (auto-loaded by BroMaker)
            if file.name not in json_assets:
                root_sounds.append(file.name)
        if root_sounds:
            assets['sounds'][''] = root_sounds  # Empty key for root level
        
        # Now get sounds from subdirectories
        for subdir in sorted(sounds_dir.iterdir()):
            if subdir.is_dir():
                subfolder_sounds = []
                for file in sorted(subdir.glob("*.wav")):
                    # Skip if it's in the JSON (auto-loaded by BroMaker)
                    if file.name not in json_assets:
                        subfolder_sounds.append(file.name)
                if subfolder_sounds:
                    assets['sounds'][subdir.name] = subfolder_sounds
    
    return assets

def generate_preload_code(bro_name):
    """Generate the PreloadAssets method code."""
    assets = find_assets(bro_name)
    
    if assets is None:
        return None
    
    code_lines = []
    code_lines.append("        public override void PreloadAssets()")
    code_lines.append("        {")
    
    # Add regular sprites
    if assets['sprites']:
        sprites_list = ', '.join(f'"{s}"' for s in assets['sprites'])
        code_lines.append(f"            CustomHero.PreloadSprites( DirectoryPath, new List<string> {{ {sprites_list} }} );")
    
    # Add projectile sprites
    if assets['projectile_sprites']:
        proj_list = ', '.join(f'"{s}"' for s in assets['projectile_sprites'])
        code_lines.append(f"            CustomHero.PreloadSprites( ProjectilePath, new List<string> {{ {proj_list} }} );")
    
    # Add sounds
    if assets['sounds']:
        # Process root level sounds first
        if '' in assets['sounds']:
            sounds_list = ', '.join(f'"{s}"' for s in assets['sounds'][''])
            code_lines.append(f"            CustomHero.PreloadSounds( SoundPath, new List<string>() {{ {sounds_list} }} );")
        
        # Process subfolder sounds
        for subfolder, sounds in sorted(assets['sounds'].items()):
            if subfolder:  # Skip empty key (root level)
                sounds_list = ', '.join(f'"{s}"' for s in sounds)
                code_lines.append(f"            CustomHero.PreloadSounds( Path.Combine( SoundPath, \"{subfolder}\" ), new List<string>() {{ {sounds_list} }} );")
    
    code_lines.append("        }")
    
    return '\n'.join(code_lines)

def main():
    if len(sys.argv) < 2:
        print("Usage: python generate_preload.py \"Bro Name\"")
        print("Example: python generate_preload.py \"Mission Impossibro\"")
        sys.exit(1)
    
    bro_name = sys.argv[1]
    print(f"Generating PreloadAssets code for {bro_name}...")
    
    code = generate_preload_code(bro_name)
    
    if code:
        print("\n" + "="*60)
        print("Generated PreloadAssets method:")
        print("="*60)
        print(code)
        print("="*60)
        
        # Try to copy to clipboard
        if copy_to_clipboard(code):
            print("\n✓ Code copied to clipboard!")
        else:
            print("\nCould not copy to clipboard. Please copy the code above manually.")
    else:
        print(f"Failed to generate code for {bro_name}")

if __name__ == "__main__":
    main()