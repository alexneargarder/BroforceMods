# Centralized Makefile for BroforceMods
# Usage: make -f ../Scripts/Makefile [target] [TYPE=mod|bro]
# Run from within any project directory

# Auto-detect project name from current directory
PROJECT_NAME := $(notdir $(CURDIR))

# Check if we're in the root directory or Scripts directory
ifeq ($(PROJECT_NAME),BroforceMods)
    IS_ROOT = true
else ifeq ($(PROJECT_NAME),Scripts)
    IS_ROOT = true
else
    IS_ROOT = false
endif

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

# Auto-detect project type by checking for CustomHero or HeroPreset (bros have these, mods don't)
PROJECT_TYPE := $(shell if grep -E 'class.*:.*CustomHero|HeroPreset' "$(PROJECT_DIR)"/*.cs 2>/dev/null >/dev/null; then echo "bro"; else echo "mod"; fi)

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
ifeq ($(IS_ROOT),true)
all: help
else
all: build
endif

# Build all projects in the repository (without launching game)
.PHONY: all-projects
all-projects:
	@echo "Building all BroforceMods projects..."
	@echo ""
	@echo "MODS:"
	@echo "========================================"
	@for dir in "Control Enemies Mod" "IronBro Multiplayer Mod" "Randomizer Mod" "Swap Bros Mod" "Utility Mod"; do \
		if [ -d "$$dir" ] || [ -d "../$$dir" ]; then \
			printf "%-30s ... " "$$dir"; \
			if [ -d "$$dir" ]; then \
				if cd "$$dir" && $(MAKE) -s -f ../Scripts/Makefile build-no-launch >/dev/null 2>&1; then \
					echo "✓"; \
				else \
					echo "✗"; \
					echo "  Errors:"; \
					cd "$$dir" && $(MAKE) -s -f ../Scripts/Makefile build-no-launch 2>&1 | grep -E "error CS|warning CS" | sed 's/^/    /'; \
				fi; \
			else \
				if cd "../$$dir" && $(MAKE) -s -f ../Scripts/Makefile build-no-launch >/dev/null 2>&1; then \
					echo "✓"; \
				else \
					echo "✗"; \
					echo "  Errors:"; \
					cd "../$$dir" && $(MAKE) -s -f ../Scripts/Makefile build-no-launch 2>&1 | grep -E "error CS|warning CS" | sed 's/^/    /'; \
				fi; \
			fi; \
		fi; \
	done
	@echo ""
	@echo "BROS:"
	@echo "========================================"
	@for dir in "Brostbuster" "Captain Ameribro" "Drunken Broster" "Furibrosa" "Mission Impossibro" "RJBrocready"; do \
		if [ -d "$$dir" ] || [ -d "../$$dir" ]; then \
			printf "%-30s ... " "$$dir"; \
			if [ -d "$$dir" ]; then \
				if cd "$$dir" && $(MAKE) -s -f ../Scripts/Makefile build-no-launch >/dev/null 2>&1; then \
					echo "✓"; \
				else \
					echo "✗"; \
					echo "  Errors:"; \
					cd "$$dir" && $(MAKE) -s -f ../Scripts/Makefile build-no-launch 2>&1 | grep -E "error CS|warning CS" | sed 's/^/    /'; \
				fi; \
			else \
				if cd "../$$dir" && $(MAKE) -s -f ../Scripts/Makefile build-no-launch >/dev/null 2>&1; then \
					echo "✓"; \
				else \
					echo "✗"; \
					echo "  Errors:"; \
					cd "../$$dir" && $(MAKE) -s -f ../Scripts/Makefile build-no-launch 2>&1 | grep -E "error CS|warning CS" | sed 's/^/    /'; \
				fi; \
			fi; \
		fi; \
	done
	@echo ""
	@echo "========================================"
	@echo "All projects built!"

# Pre-build: Kill Broforce if running
.PHONY: pre-build
pre-build:
	@taskkill.exe /f /t /im Broforce_beta.exe 2>/dev/null || true

# Build the project
.PHONY: build
build:
ifeq ($(IS_ROOT),true)
	@echo "Error: Not in a project directory."
	@echo "Run from within a project directory or use 'make -f Scripts/Makefile all-projects'"
	@echo ""
	@$(MAKE) -s -f $(MAKEFILE_LIST) help
else
	@$(MAKE) -s -f $(MAKEFILE_LIST) pre-build
	@echo "Building $(PROJECT_NAME)..."
	@"$(MSBUILD)" "$(PROJECT_FILE_WIN)" /p:Configuration=Debug /p:PreBuildEvent="" /p:PostBuildEvent="" /nologo /verbosity:quiet /noconlog || \
		("$(MSBUILD)" "$(PROJECT_FILE_WIN)" /p:Configuration=Debug /p:PreBuildEvent="" /p:PostBuildEvent="" /nologo /verbosity:minimal && false)
	@$(MAKE) -s -f $(MAKEFILE_LIST) post-build
endif

# Build without launching Broforce
.PHONY: build-no-launch
build-no-launch:
ifeq ($(IS_ROOT),true)
	@echo "Error: Not in a project directory."
	@echo "Run from within a project directory or use 'make -f Scripts/Makefile all-projects'"
	@echo ""
	@$(MAKE) -s -f $(MAKEFILE_LIST) help
else
	@$(MAKE) -s -f $(MAKEFILE_LIST) pre-build
	@echo "Building $(PROJECT_NAME)..."
	@"$(MSBUILD)" "$(PROJECT_FILE_WIN)" /p:Configuration=Debug /p:PreBuildEvent="" /p:PostBuildEvent="" /nologo /verbosity:quiet /noconlog || \
		("$(MSBUILD)" "$(PROJECT_FILE_WIN)" /p:Configuration=Debug /p:PreBuildEvent="" /p:PostBuildEvent="" /nologo /verbosity:minimal && false)
	@$(MAKE) -s -f $(MAKEFILE_LIST) post-build-no-launch
endif

# Post-build: Copy files and optionally launch Steam
.PHONY: post-build
post-build:
	@mkdir -p "$(DEST_DIR)"
	@rm -f "$(DEST_DIR)"/*.cache 2>/dev/null || true
	@cp -f "$(TARGET_DLL)" "$(DEST_DIR)/$(DEST_DLL_NAME)"
	@cmd.exe /c start steam://rungameid/274190

# Post-build without launching Broforce
.PHONY: post-build-no-launch
post-build-no-launch:
	@mkdir -p "$(DEST_DIR)"
	@rm -f "$(DEST_DIR)"/*.cache 2>/dev/null || true
	@cp -f "$(TARGET_DLL)" "$(DEST_DIR)/$(DEST_DLL_NAME)"

# Clean build
.PHONY: clean
clean:
ifeq ($(IS_ROOT),true)
	@echo "Error: Not in a project directory."
	@echo "Run from within a project directory"
	@echo ""
	@$(MAKE) -s -f $(MAKEFILE_LIST) help
else
	@echo "Cleaning $(PROJECT_NAME)..."
	@"$(MSBUILD)" "$(PROJECT_FILE_WIN)" /t:Clean /p:Configuration=Debug
	@echo "Clean complete!"
endif

# Rebuild (clean + build)
.PHONY: rebuild
rebuild:
ifeq ($(IS_ROOT),true)
	@echo "Error: Not in a project directory."
	@echo "Run from within a project directory"
	@echo ""
	@$(MAKE) -s -f $(MAKEFILE_LIST) help
else
	@$(MAKE) -s -f $(MAKEFILE_LIST) clean
	@$(MAKE) -s -f $(MAKEFILE_LIST) build
endif

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
	@echo "  make all-projects           - Build all projects (without launching)"
	@echo ""
	@echo "Options:"
	@echo "  TYPE=mod|bro      - Override auto-detected project type"
	@echo ""
	@echo "The Makefile auto-detects:"
	@echo "  - Project name from current directory"
	@echo "  - Project type from BroMakerLib reference (bro) or default (mod)"