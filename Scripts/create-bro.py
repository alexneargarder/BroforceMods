import shutil, errno
import os, fnmatch
import sys

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
            with open(filepath) as f:
                s = f.read()
            s = s.replace(find, replace)
            with open(filepath, "w") as f:
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

newName = input('Enter bro name:\n')
newNameWithUnderscore = newName.replace(' ', '_')
newNameNoSpaces = newName.replace(' ', '')

templatePath = os.path.join(repoPath, 'BroforceMods', 'Bro Template')
modTemplatePath = os.path.join(repoPath, 'BroforceMods', 'Scripts', 'Bro Template')
# Create the release folder structure
releasesPath = os.path.join(repoPath, 'BroforceMods', 'Releases')
newBroReleasePath = os.path.join(releasesPath, newName)
newBroModPath = os.path.join(newBroReleasePath, newName)
newRepoPath = os.path.join(repoPath, 'BroforceMods', newName)

# Check if template directories exist
if not os.path.exists(templatePath):
    print(f"Error: Template directory not found: {templatePath}")
    print("Please ensure the 'Bro Template' directory exists in your repository.")
    sys.exit(1)

if not os.path.exists(modTemplatePath):
    print(f"Error: Mod template directory not found: {modTemplatePath}")
    print("Please ensure the 'Scripts/Bro Template' directory exists in your repository.")
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
if os.path.exists(newBroReleasePath):
    print(f"Error: Release directory already exists: {newBroReleasePath}")
    print("Please choose a different bro name or remove the existing directory.")
    sys.exit(1)

if os.path.exists(newRepoPath):
    print(f"Error: Repository directory already exists: {newRepoPath}")
    print("Please choose a different bro name or remove the existing directory.")
    sys.exit(1)

try:
    # Create the release folder first
    os.makedirs(newBroReleasePath)
    # Copy mod template to the nested folder structure
    copyanything(modTemplatePath, newBroModPath)
    # Copy the source template to the repo
    copyanything(templatePath, newRepoPath)
except Exception as e:
    print(f"Error: Failed to copy template files: {e}")
    # Clean up partially created directories
    if os.path.exists(newBroReleasePath):
        shutil.rmtree(newBroReleasePath)
    if os.path.exists(newRepoPath):
        shutil.rmtree(newRepoPath)
    sys.exit(1)

try:
    # Rename files named "Bro Template" (with space)
    renameFiles(newRepoPath, 'Bro Template', newName)
    renameFiles(newBroModPath, 'Bro Template', newName)
    
    # Also rename files named "BroTemplate" (without space)
    renameFiles(newRepoPath, 'BroTemplate', newNameNoSpaces)
    renameFiles(newBroModPath, 'BroTemplate', newNameNoSpaces)

    fileTypes = ["*.csproj", "*.cs", "*.sln", "*.bat", "*.json"]
    for fileType in fileTypes:
        findReplace(newRepoPath, "Bro Template", newName, fileType)
        findReplace(newRepoPath, "Bro_Template", newNameWithUnderscore, fileType)
        findReplace(newRepoPath, "BroTemplate", newNameNoSpaces, fileType)

        findReplace(newBroModPath, "Bro Template", newName, fileType)
        findReplace(newBroModPath, "Bro_Template", newNameWithUnderscore, fileType)
        findReplace(newBroModPath, "BroTemplate", newNameNoSpaces, fileType)
    
    # Special handling for .csproj file references
    findReplace(newRepoPath, "BroTemplate.cs", f"{newNameNoSpaces}.cs", "*.csproj")
    findReplace(newBroModPath, "BroTemplate.cs", f"{newNameNoSpaces}.cs", "*.csproj")
    
    # Create the CREATE LINK.bat file in the Releases folder
    batFilePath = os.path.join(newBroReleasePath, 'CREATE LINK.bat')
    batContent = f'mklink /D "%BROFORCEPATH%\\BroMaker_Storage\\{newName}" "%REPOSPATH%\\BroforceMods\\Releases\\{newName}\\{newName}"\npause'
    
    with open(batFilePath, 'w') as batFile:
        batFile.write(batContent)
    
    print(f"\nSuccess! Created new bro '{newName}'")
    print(f"Source files: {newRepoPath}")
    print(f"Release files: {newBroModPath}")
    print(f"\nNote: Run the CREATE LINK.bat script to create the symlink:")
    print(f"  {batFilePath}")
    
except Exception as e:
    print(f"Error: Failed during file processing: {e}")
    sys.exit(1)
