import shutil, errno
import os, fnmatch

def copyanything(src, dst):
    try:
        shutil.copytree(src, dst)
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

broforcePath = r'D:\Steam\steamapps\common\Broforce\BroMaker_Storage\Bros'
repoPath = r'C:\Users\Alex\Desktop\Coding Things\Github\BroforceModsDev'
newName = input('Enter bro name:\n')
newNameWithUnderscore = newName.replace(' ', '_')

templatePath = os.path.join(repoPath, 'Bro Template')
modTemplatePath = os.path.join(repoPath, r'Scripts\Bro Template')
newBroforcePath = os.path.join(broforcePath, newName)
newRepoPath = os.path.join(repoPath, newName)

copyanything(modTemplatePath, newBroforcePath)
copyanything(templatePath, newRepoPath)

renameFiles(newRepoPath, 'Bro Template', newName)
renameFiles(newBroforcePath, 'Bro Template', newName)

fileTypes = ["*.csproj", "*.cs", "*.sln", "*.bat", "*.json"]
for fileType in fileTypes:
    findReplace(newRepoPath, "Bro Template", newName, fileType)
    findReplace(newRepoPath, "Bro_Template", newNameWithUnderscore, fileType)

    findReplace(newBroforcePath, "Bro Template", newName, fileType)
    findReplace(newBroforcePath, "Bro_Template", newNameWithUnderscore, fileType)
