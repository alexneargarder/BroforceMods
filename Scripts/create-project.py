import shutil, errno
import os, fnmatch
import sys
import re
import argparse
import json

def copyanything(src, dst):
    def ignore_patterns(path, names):
        # Ignore Visual Studio user-specific files and folders
        ignored = []
        for name in names:
            if name == '.vs' or name.endswith('.suo') or name.endswith('.user'):
                ignored.append(name)
        return ignored
    
    try:
        shutil.copytree(src, dst, ignore=ignore_patterns)
    except OSError as exc: # python >2.5
        if exc.errno in (errno.ENOTDIR, errno.EINVAL):
            shutil.copy(src, dst)
        else: raise

def findReplace(directory, find, replace, filePattern):
    for path, dirs, files in os.walk(os.path.abspath(directory)):
        for filename in fnmatch.filter(files, filePattern):
            filepath = os.path.join(path, filename)
            with open(filepath, encoding='utf-8') as f:
                s = f.read()
            s = s.replace(find, replace)
            with open(filepath, "w", encoding='utf-8') as f:
                f.write(s)
        for dir in dirs:
            findReplace(os.path.join(path, dir), find, replace, filePattern)

def renameFiles(directory, find, replace):
    for path, dirs, files in os.walk(os.path.abspath(directory)):
        for filename in fnmatch.filter(files, find + '.*'):
            filepath = os.path.join(path, filename)
            os.rename(filepath, os.path.join(path, replace) + '.' + filename.partition('.')[2])
        for dir in dirs:
            filepath = os.path.join(path, dir)
            if dir == find:
                os.rename(filepath, os.path.join(path, replace))
                renameFiles(os.path.join(path, replace), find, replace)
            else:
                renameFiles(filepath, find, replace)

# Parse command line arguments
parser = argparse.ArgumentParser(
    description='Create a new Broforce mod or bro project from templates',
    formatter_class=argparse.RawDescriptionHelpFormatter,
    epilog='''Examples:
  %(prog)s                     # Interactive mode
  %(prog)s -t mod -n "My Mod" -a "MyName"
  %(prog)s --type bro --name "Super Bro" --author "CoolDev"
'''
)
parser.add_argument('-t', '--type', choices=['mod', 'bro'], help='Project type (mod or bro)')
parser.add_argument('-n', '--name', help='Name of the mod or bro')
parser.add_argument('-a', '--author', help='Author name')
args = parser.parse_args()

# Get the repository name dynamically based on script location
script_dir = os.path.dirname(os.path.abspath(__file__))
repo_dir = os.path.dirname(script_dir)
repo_name = os.path.basename(repo_dir)

# Check for required environment variables
broforcePath = os.environ.get('BROFORCEPATH')
repoPath = os.environ.get('REPOSPATH')

if not broforcePath:
    print("Error: BROFORCEPATH environment variable is not set.")
    print("Please set BROFORCEPATH to the path of your Broforce folder.")
    print("\nFor PowerShell (temporary):")
    print('  $env:BROFORCEPATH = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Broforce"')
    print("\nFor Command Prompt (temporary):")
    print('  set BROFORCEPATH="C:\\Program Files (x86)\\Steam\\steamapps\\common\\Broforce"')
    print("\nTo set permanently:")
    print('  1. Search "environment variables" in the Windows Start Menu')
    print('  2. Click "Edit the system environment variables"')
    print('  3. Click "Environment Variables..." button')
    print('  4. Under "User variables", click "New..."')
    print('  5. Set Variable name: BROFORCEPATH')
    print('  6. Set Variable value: your Broforce folder path')
    print('  7. Click OK and restart your terminal')
    sys.exit(1)

if not repoPath:
    print("Error: REPOSPATH environment variable is not set.")
    print("Please set REPOSPATH to the path of your repositories folder.")
    print("\nFor PowerShell (temporary):")
    print('  $env:REPOSPATH = "C:\\Users\\YourName\\repos"')
    print("\nFor Command Prompt (temporary):")
    print('  set REPOSPATH=C:\\Users\\YourName\\repos')
    print("\nTo set permanently:")
    print('  1. Search "environment variables" in the Windows Start Menu')
    print('  2. Click "Edit the system environment variables"')
    print('  3. Click "Environment Variables..." button')
    print('  4. Under "User variables", click "New..."')
    print('  5. Set Variable name: REPOSPATH')
    print('  6. Set Variable value: your repositories folder path')
    print('  7. Click OK and restart your terminal')
    sys.exit(1)

# Check if environment variable paths exist
if not os.path.exists(broforcePath):
    print(f"Error: BROFORCEPATH directory does not exist: {broforcePath}")
    print("Please check that the BROFORCEPATH environment variable points to a valid directory.")
    sys.exit(1)

if not os.path.exists(repoPath):
    print(f"Error: REPOSPATH directory does not exist: {repoPath}")
    print("Please check that the REPOSPATH environment variable points to a valid directory.")
    sys.exit(1)

# Get project type
if args.type:
    template_type = args.type
else:
    # Ask what type of template to create
    print("What would you like to create?")
    print("1. Mod")
    print("2. Bro")
    choice = input("Enter your choice (1 or 2): ").strip()
    
    if choice == "1":
        template_type = "mod"
    elif choice == "2":
        template_type = "bro"
    else:
        print("Invalid choice. Please run the script again and enter 1 or 2.")
        sys.exit(1)

# Set template parameters based on type
if template_type == "mod":
    template_type_title = "Mod"
    source_template_name = "Mod Template"
    install_template_name = "Mod Install Template"
    symlink_path = "mods"
else:
    template_type_title = "Bro"
    source_template_name = "Bro Template"
    install_template_name = "Bro Template"
    symlink_path = "BroMaker_Storage"

# Get the name for the new template
if args.name:
    newName = args.name
else:
    newName = input(f'Enter {template_type} name:\n')

newNameWithUnderscore = newName.replace(' ', '_')
newNameNoSpaces = newName.replace(' ', '')

# Get the author name
if args.author:
    authorName = args.author
else:
    authorName = input('Enter author name (e.g., YourName):\n')

# Define paths
templatePath = os.path.join(repo_dir, source_template_name)
modTemplatePath = os.path.join(repo_dir, 'Scripts', install_template_name)
# Create the release folder structure
releasesPath = os.path.join(repo_dir, 'Releases')
newReleasePathOuter = os.path.join(releasesPath, newName)
newReleasePathInner = os.path.join(newReleasePathOuter, newName)
newRepoPath = os.path.join(repo_dir, newName)

# Check if template directories exist
if not os.path.exists(templatePath):
    print(f"Error: Template directory not found: {templatePath}")
    print(f"Please ensure the '{source_template_name}' directory exists in your repository.")
    sys.exit(1)

if not os.path.exists(modTemplatePath):
    print(f"Error: Mod template directory not found: {modTemplatePath}")
    print(f"Please ensure the 'Scripts/{install_template_name}' directory exists in your repository.")
    sys.exit(1)

# Create Releases directory if it doesn't exist
if not os.path.exists(releasesPath):
    try:
        os.makedirs(releasesPath)
        print(f"Created Releases directory: {releasesPath}")
    except Exception as e:
        print(f"Error: Failed to create Releases directory: {e}")
        sys.exit(1)

# Check if destination directories already exist
if os.path.exists(newReleasePathOuter):
    print(f"Error: Release directory already exists: {newReleasePathOuter}")
    print(f"Please choose a different {template_type} name or remove the existing directory.")
    sys.exit(1)

if os.path.exists(newRepoPath):
    print(f"Error: Repository directory already exists: {newRepoPath}")
    print(f"Please choose a different {template_type} name or remove the existing directory.")
    sys.exit(1)

try:
    # Create the release folder first
    os.makedirs(newReleasePathOuter)
    # Copy mod template to the nested folder structure
    copyanything(modTemplatePath, newReleasePathInner)
    # Copy the source template to the repo
    copyanything(templatePath, newRepoPath)
except Exception as e:
    print(f"Error: Failed to copy template files: {e}")
    # Clean up partially created directories
    if os.path.exists(newReleasePathOuter):
        shutil.rmtree(newReleasePathOuter)
    if os.path.exists(newRepoPath):
        shutil.rmtree(newRepoPath)
    sys.exit(1)

try:
    # Rename files named with template name (with space)
    renameFiles(newRepoPath, source_template_name, newName)
    renameFiles(newReleasePathInner, source_template_name, newName)
    
    # Also rename files named without space
    if template_type == "mod":
        renameFiles(newRepoPath, 'ModTemplate', newNameNoSpaces)
        renameFiles(newReleasePathInner, 'ModTemplate', newNameNoSpaces)
    else:
        renameFiles(newRepoPath, 'BroTemplate', newNameNoSpaces)
        renameFiles(newReleasePathInner, 'BroTemplate', newNameNoSpaces)

    # File types to process
    if template_type == "mod":
        fileTypes = ["*.csproj", "*.cs", "*.sln", "*.bat", "*.json", "*.xml"]
    else:
        fileTypes = ["*.csproj", "*.cs", "*.sln", "*.bat", "*.json"]
    
    for fileType in fileTypes:
        # Replace template names
        findReplace(newRepoPath, source_template_name, newName, fileType)
        findReplace(newRepoPath, source_template_name.replace(' ', '_'), newNameWithUnderscore, fileType)
        if template_type == "mod":
            findReplace(newRepoPath, "ModTemplate", newNameNoSpaces, fileType)
        else:
            findReplace(newRepoPath, "BroTemplate", newNameNoSpaces, fileType)

        findReplace(newReleasePathInner, source_template_name, newName, fileType)
        findReplace(newReleasePathInner, source_template_name.replace(' ', '_'), newNameWithUnderscore, fileType)
        if template_type == "mod":
            findReplace(newReleasePathInner, "ModTemplate", newNameNoSpaces, fileType)
        else:
            findReplace(newReleasePathInner, "BroTemplate", newNameNoSpaces, fileType)
        
        # Replace author placeholder
        findReplace(newRepoPath, "AUTHOR_NAME", authorName, fileType)
        findReplace(newReleasePathInner, "AUTHOR_NAME", authorName, fileType)
        
        # Replace repository URL
        findReplace(newRepoPath, "REPO_NAME", repo_name, fileType)
        findReplace(newReleasePathInner, "REPO_NAME", repo_name, fileType)
    
    # Special handling for .csproj file references for Bros
    if template_type == "bro":
        findReplace(newRepoPath, "BroTemplate.cs", f"{newNameNoSpaces}.cs", "*.csproj")
        findReplace(newReleasePathInner, "BroTemplate.cs", f"{newNameNoSpaces}.cs", "*.csproj")
        
        # Get BroMaker version from its Info.json
        bromaker_info_path = os.path.join(broforcePath, "Mods", "BroMaker", "Info.json")
        bromaker_version = "2.6.0"  # Default fallback version
        
        if os.path.exists(bromaker_info_path):
            try:
                import json
                with open(bromaker_info_path, 'r', encoding='utf-8') as f:
                    bromaker_info = json.load(f)
                    bromaker_version = bromaker_info.get('Version', bromaker_version)
                    print(f"Detected BroMaker version: {bromaker_version}")
            except Exception as e:
                print(f"Warning: Could not read BroMaker version from Info.json: {e}")
                print(f"Using default BroMaker version: {bromaker_version}")
        else:
            print(f"Warning: BroMaker Info.json not found at {bromaker_info_path}")
            print(f"Using default BroMaker version: {bromaker_version}")
        
        # Replace BroMaker version placeholder
        findReplace(newRepoPath, "BROMAKER_VERSION", bromaker_version, "*.json")
        findReplace(newReleasePathInner, "BROMAKER_VERSION", bromaker_version, "*.json")
        
        # Check for BroMakerLib and update the reference path
        # First try the local Bro-Maker repo path
        local_bromaker_path = os.path.join(repoPath, "Bro-Maker", "BroMakerLib", "bin", "Debug", "BroMakerLib.dll")
        if os.path.exists(local_bromaker_path):
            print(f"Found local BroMakerLib at: {local_bromaker_path}")
            print("Using local development version of BroMakerLib")
            
            # Update .csproj files to use the local development path
            csproj_files = []
            for root, dirs, files in os.walk(newRepoPath):
                for file in files:
                    if file.endswith('.csproj'):
                        csproj_files.append(os.path.join(root, file))
            
            for csproj_file in csproj_files:
                with open(csproj_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                # Calculate relative path from project to local BroMakerLib
                project_dir = os.path.dirname(csproj_file)
                rel_path = os.path.relpath(local_bromaker_path, project_dir).replace('/', '\\')
                
                # Replace the HintPath for BroMakerLib
                pattern = r'(<Reference Include="BroMakerLib[^>]*>.*?<HintPath>)(.*?)(</HintPath>)'
                replacement = rf'\1{rel_path}\3'
                content = re.sub(pattern, replacement, content, flags=re.DOTALL)
                
                with open(csproj_file, 'w', encoding='utf-8') as f:
                    f.write(content)
                    
            print(f"Updated BroMakerLib reference to use local development version")
        else:
            # Check if the installed version exists
            installed_bromaker_path = os.path.join(broforcePath, "Mods", "BroMaker", "BroMakerLib.dll")
            
            if os.path.exists(installed_bromaker_path):
                print("Found installed BroMakerLib.dll in Mods folder")
            else:
                print("Warning: Could not find BroMakerLib.dll in either location:")
                print(f"  - Local repo: {local_bromaker_path}")
                print(f"  - Installed: {installed_bromaker_path}")
                print("Make sure BroMaker is installed in your Mods folder")
    
    # Create the Changelog.txt file in the Releases folder
    changelogPath = os.path.join(newReleasePathOuter, 'Changelog.txt')
    changelogContent = 'v1.0.0\nRelease'
    
    with open(changelogPath, 'w', encoding='utf-8') as changelogFile:
        changelogFile.write(changelogContent)
    
    print(f"\nSuccess! Created new {template_type} '{newName}'")
    print(f"Source files: {newRepoPath}")
    print(f"Release files: {newReleasePathInner}")
    print(f"\nNote: Run the CREATE LINKS.bat script in the Releases folder to create symlinks")
    
except Exception as e:
    print(f"Error: Failed during file processing: {e}")
    sys.exit(1)