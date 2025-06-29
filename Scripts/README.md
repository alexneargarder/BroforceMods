# Broforce Templates

This repository provides templates and scripts for creating Broforce mods and custom bros.

## Setup
Set these environment variables:
- `BROFORCEPATH` - Path to your Broforce installation (e.g., `C:\Program Files (x86)\Steam\steamapps\common\Broforce`)
- `REPOSPATH` - Path to your repositories folder

## Creating Projects

Use `create-project.py` to generate new mods or custom bros from the templates.

### Usage
```bash
# Interactive mode
python create-project.py

# Command line mode
python create-project.py -t mod -n "My Mod" -a "YourName"
python create-project.py --type bro --name "My Bro" --author "YourName"
```

### Options
- `-h, --help` - Show help message
- `-t, --type` - Project type: `mod` or `bro`
- `-n, --name` - Project name
- `-a, --author` - Author name

The script will:
1. Create source files in the repository root
2. Create release files in `Releases/[ProjectName]/`
3. Generate a Changelog.txt
4. Configure BroMakerLib references (for bro projects)

Run `CREATE LINKS.bat` in the Releases folder to create symlinks to your mods.