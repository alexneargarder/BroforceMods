# Makefile for Utility Mod
# Handles building from WSL with proper Windows path conversion

# Paths
MSBUILD = "/mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
PROJECT = "C:\\Users\\Alex\\repos\\BroforceMods\\Utility Mod\\Utility Mod\\Utility Mod.csproj"
BIN_DIR = /mnt/c/Users/Alex/repos/BroforceMods/Utility Mod/Utility Mod/bin/Debug
BROFORCE_MOD_DIR = "/mnt/c/Program Files (x86)/Steam/steamapps/common/Broforce/Mods/Utility Mod"

# Default target
.PHONY: all
all: build

# Kill Broforce if running (pre-build)
.PHONY: kill-broforce
kill-broforce:
	@echo "Checking for running Broforce process..."
	@taskkill.exe /f /t /im Broforce_beta.exe 2>/dev/null || true

# Build the project
.PHONY: build
build: kill-broforce
	@echo "Building Utility Mod..."
	@$(MSBUILD) $(PROJECT) /p:Configuration=Debug /p:PreBuildEvent="" /p:PostBuildEvent=""
	@$(MAKE) post-build

# Post-build steps
.PHONY: post-build
post-build:
	@echo "Copying Utility Mod.dll to Broforce mods directory..."
	@mkdir -p $(BROFORCE_MOD_DIR)
	@cp -f "$(BIN_DIR)/Utility Mod.dll" $(BROFORCE_MOD_DIR)/
	@echo "Cleaning Broforce mod cache..."
	@rm -f $(BROFORCE_MOD_DIR)/*.cache 2>/dev/null || true
	@echo "Post-build complete!"

# Clean build
.PHONY: clean
clean:
	@echo "Cleaning build output..."
	@$(MSBUILD) $(PROJECT) /t:Clean /p:Configuration=Debug
	@echo "Clean complete!"

# Rebuild (clean + build)
.PHONY: rebuild
rebuild: clean build

# Build without killing Broforce
.PHONY: build-no-kill
build-no-kill:
	@echo "Building Utility Mod..."
	@$(MSBUILD) $(PROJECT) /p:Configuration=Debug /p:PreBuildEvent="" /p:PostBuildEvent=""
	@$(MAKE) post-build
	@echo "Build complete (without killing Broforce)!"

# Build Release configuration
.PHONY: release
release: kill-broforce
	@echo "Building Utility Mod (Release)..."
	@$(MSBUILD) $(PROJECT) /p:Configuration=Release /p:PreBuildEvent="" /p:PostBuildEvent=""
	@echo "Copying Utility Mod.dll to Broforce mods directory..."
	@mkdir -p $(BROFORCE_MOD_DIR)
	@cp -f "/mnt/c/Users/Alex/repos/BroforceMods/Utility Mod/Utility Mod/bin/Release/Utility Mod.dll" $(BROFORCE_MOD_DIR)/
	@rm -f $(BROFORCE_MOD_DIR)/*.cache 2>/dev/null || true
	@echo "Release build complete!"

# Help
.PHONY: help
help:
	@echo "Utility Mod Makefile targets:"
	@echo "  make              - Build the project (default)"
	@echo "  make clean        - Clean build output"
	@echo "  make rebuild      - Clean and rebuild the project"
	@echo "  make release      - Build Release configuration"
	@echo "  make build-no-kill - Build without killing Broforce"
	@echo "  make help         - Show this help message"