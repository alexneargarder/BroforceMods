# Centralized Makefile for BroforceMods
# Usage: make -f ../Scripts/Makefile [target] [TYPE=mod|bro]
# Run from within any project directory

# Auto-detect project name from current directory
PROJECT_NAME := $(notdir $(CURDIR))

# Handle special case for Captain Ameribro
ifeq ($(PROJECT_NAME),Captain Ameribro)
    PROJECT_DIR = Captain Ameribro Mod
    BUILD_DLL_NAME = Captain Ameribro Mod.dll
    DEST_DLL_NAME = Captain Ameribro.dll
else
    PROJECT_DIR = $(PROJECT_NAME)
    BUILD_DLL_NAME = $(PROJECT_NAME).dll
    DEST_DLL_NAME = $(PROJECT_NAME).dll
endif

# Paths
MSBUILD = /mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe
PROJECT_FILE = $(PROJECT_DIR)/$(PROJECT_NAME).csproj
TARGET_DLL = $(PROJECT_DIR)/bin/Debug/$(BUILD_DLL_NAME)
BROFORCE_PATH = /mnt/c/Program Files (x86)/Steam/steamapps/common/Broforce

# Convert WSL path to Windows path
PROJECT_DIR_WIN = $(shell wslpath -w "$(CURDIR)")
PROJECT_FILE_WIN = $(PROJECT_DIR_WIN)\\$(PROJECT_DIR)\\$(PROJECT_NAME).csproj

# Auto-detect project type by checking for BroMakerLib reference
PROJECT_TYPE := $(shell grep -q "BroMakerLib" "$(PROJECT_FILE)" 2>/dev/null && echo "bro" || echo "mod")

# Allow TYPE override
ifdef TYPE
    PROJECT_TYPE := $(TYPE)
endif

# Set destination based on project type
ifeq ($(PROJECT_TYPE),bro)
    DEST_DIR = $(BROFORCE_PATH)/BroMaker_Storage/$(PROJECT_NAME)
else
    DEST_DIR = $(BROFORCE_PATH)/Mods/$(PROJECT_NAME)
endif

# Default target
.PHONY: all
all: build

# Build all projects in the repository
.PHONY: all-projects
all-projects:
	@echo "Building all BroforceMods projects..."
	@echo "========================================"
	@for dir in "Brostbuster" "Captain Ameribro" "Control Enemies Mod" "Drunken Broster" "Furibrosa" "IronBro Multiplayer Mod" "Mission Impossibro" "RJBrocready" "Randomizer Mod" "Swap Bros Mod" "Utility Mod"; do \
		if [ -d "$$dir" ] || [ -d "../$$dir" ]; then \
			echo ""; \
			echo "Building $$dir..."; \
			echo "----------------------------------------"; \
			if [ -d "$$dir" ]; then \
				(cd "$$dir" && $(MAKE) -f ../Scripts/Makefile build) || echo "Failed to build $$dir"; \
			else \
				(cd "../$$dir" && $(MAKE) -f ../Scripts/Makefile build) || echo "Failed to build $$dir"; \
			fi; \
		fi; \
	done
	@echo ""
	@echo "========================================"
	@echo "All projects built!"

# Build all projects without launching Broforce
.PHONY: all-projects-no-launch
all-projects-no-launch:
	@echo "Building all BroforceMods projects (no launch)..."
	@echo "========================================"
	@for dir in "Brostbuster" "Captain Ameribro" "Control Enemies Mod" "Drunken Broster" "Furibrosa" "IronBro Multiplayer Mod" "Mission Impossibro" "RJBrocready" "Randomizer Mod" "Swap Bros Mod" "Utility Mod"; do \
		if [ -d "$$dir" ] || [ -d "../$$dir" ]; then \
			echo ""; \
			echo "Building $$dir..."; \
			echo "----------------------------------------"; \
			if [ -d "$$dir" ]; then \
				(cd "$$dir" && $(MAKE) -f ../Scripts/Makefile build-no-launch) || echo "Failed to build $$dir"; \
			else \
				(cd "../$$dir" && $(MAKE) -f ../Scripts/Makefile build-no-launch) || echo "Failed to build $$dir"; \
			fi; \
		fi; \
	done
	@echo ""
	@echo "========================================"
	@echo "All projects built!"

# Pre-build: Kill Broforce if running
.PHONY: pre-build
pre-build:
	@echo "Running pre-build for $(PROJECT_TYPE): $(PROJECT_NAME)..."
	@echo "Killing Broforce_beta.exe if running..."
	@taskkill.exe /f /t /im Broforce_beta.exe 2>/dev/null || true

# Build the project
.PHONY: build
build: pre-build
	@echo "Building $(PROJECT_NAME) (detected as: $(PROJECT_TYPE))..."
	@"$(MSBUILD)" "$(PROJECT_FILE_WIN)" /p:Configuration=Debug /p:PreBuildEvent="" /p:PostBuildEvent=""
	@$(MAKE) -f $(MAKEFILE_LIST) post-build

# Build without launching Broforce
.PHONY: build-no-launch
build-no-launch: pre-build
	@echo "Building $(PROJECT_NAME) (detected as: $(PROJECT_TYPE))..."
	@"$(MSBUILD)" "$(PROJECT_FILE_WIN)" /p:Configuration=Debug /p:PreBuildEvent="" /p:PostBuildEvent=""
	@$(MAKE) -f $(MAKEFILE_LIST) post-build-no-launch

# Post-build: Copy files and optionally launch Steam
.PHONY: post-build
post-build:
	@echo "Running post-build..."
	@echo "Creating destination directory: $(DEST_DIR)"
	@mkdir -p "$(DEST_DIR)"
	@echo "Removing cache files..."
	@rm -f "$(DEST_DIR)"/*.cache 2>/dev/null || true
	@echo "Copying $(BUILD_DLL_NAME) to $(DEST_DIR)/$(DEST_DLL_NAME)..."
	@cp -f "$(TARGET_DLL)" "$(DEST_DIR)/$(DEST_DLL_NAME)"
	@echo "Launching Broforce via Steam..."
	@cmd.exe /c start steam://rungameid/274190
	@echo "Build complete!"

# Post-build without launching Broforce
.PHONY: post-build-no-launch
post-build-no-launch:
	@echo "Running post-build..."
	@echo "Creating destination directory: $(DEST_DIR)"
	@mkdir -p "$(DEST_DIR)"
	@echo "Removing cache files..."
	@rm -f "$(DEST_DIR)"/*.cache 2>/dev/null || true
	@echo "Copying $(BUILD_DLL_NAME) to $(DEST_DIR)/$(DEST_DLL_NAME)..."
	@cp -f "$(TARGET_DLL)" "$(DEST_DIR)/$(DEST_DLL_NAME)"
	@echo "Build complete!"

# Clean build
.PHONY: clean
clean:
	@echo "Cleaning $(PROJECT_NAME)..."
	@"$(MSBUILD)" "$(PROJECT_FILE_WIN)" /t:Clean /p:Configuration=Debug
	@echo "Clean complete!"

# Rebuild (clean + build)
.PHONY: rebuild
rebuild: clean build

# Show detected configuration
.PHONY: info
info:
	@echo "Project Name: $(PROJECT_NAME)"
	@echo "Project Type: $(PROJECT_TYPE)"
	@echo "Project Dir: $(PROJECT_DIR)"
	@echo "Project File: $(PROJECT_FILE)"
	@echo "Build DLL: $(BUILD_DLL_NAME)"
	@echo "Dest DLL: $(DEST_DLL_NAME)"
	@echo "Target DLL: $(TARGET_DLL)"
	@echo "Destination: $(DEST_DIR)"
	@echo "Broforce Path: $(BROFORCE_PATH)"

# Help
.PHONY: help
help:
	@echo "Centralized BroforceMods Makefile"
	@echo ""
	@echo "Usage: make -f ../Scripts/Makefile [target] [TYPE=mod|bro]"
	@echo ""
	@echo "Per-Project Targets:"
	@echo "  make                  - Build the project (default)"
	@echo "  make build-no-launch  - Build without launching Broforce"
	@echo "  make clean            - Clean build output"
	@echo "  make rebuild          - Clean and rebuild"
	@echo "  make info             - Show detected configuration"
	@echo "  make help             - Show this help"
	@echo ""
	@echo "All-Projects Targets (run from Scripts or root directory):"
	@echo "  make all-projects           - Build all projects"
	@echo "  make all-projects-no-launch - Build all projects without launching"
	@echo ""
	@echo "Options:"
	@echo "  TYPE=mod|bro      - Override auto-detected project type"
	@echo ""
	@echo "The Makefile auto-detects:"
	@echo "  - Project name from current directory"
	@echo "  - Project type from BroMakerLib reference (bro) or default (mod)"