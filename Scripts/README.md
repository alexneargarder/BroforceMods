# create-bro.py

Creates a new custom character mod from template files.

## Setup
Set these environment variables:
- `BROPATH` - Path to BroMaker_Storage folder
- `REPOS` - Path to your repos folder, where this repo is located

## Usage
```
python create-bro.py
```
Enter your bro name when prompted. The script will create source and release files, then run the CREATE LINK.bat in the release folder.

# create-mod.py

Creates a new general mod from template files.

## Setup
Set these environment variables:
- `BROFORCEMODS` - Path to Broforce Mods folder
- `REPOS` - Path to your repos folder (same as above)

## Usage
```
python create-mod.py
```
Enter your mod name when prompted. Creates the same folder structure as create-bro.py.