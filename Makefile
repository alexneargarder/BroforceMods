# BroforceMods Build System

# Default target
.DEFAULT_GOAL := all

# Environment detection
IS_NIXOS := $(shell grep -q "NixOS" /etc/os-release 2>/dev/null && echo 1 || echo 0)
IS_WSL := $(shell grep -qi microsoft /proc/version 2>/dev/null && echo 1 || echo 0)
IN_DEVENV := $(shell [ -n "$$DEVENV_PROFILE" ] && echo 1)

# Platform-specific configuration
ifeq ($(IS_NIXOS),1)
    PLATFORM := nixos
    ifeq ($(IN_DEVENV),1)
        MSBUILD := msbuild
    else
        MSBUILD := devenv shell -- msbuild
    endif
    GAME_PATH := $(HOME)/.local/share/Steam/steamapps/common/Broforce
else ifeq ($(IS_WSL),1)
    PLATFORM := wsl
    MSBUILD := "/mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
    GAME_PATH := /mnt/c/Program Files (x86)/Steam/steamapps/common/Broforce
else
    $(error Unsupported platform. This Makefile requires NixOS or WSL)
endif

# Common MSBuild flags
MSBUILD_FLAGS := /p:Configuration=Release /verbosity:minimal /nologo /p:PreBuildEvent="" /p:PostBuildEvent="" /m

# Installation paths
MODS_PATH := $(GAME_PATH)/Mods
BROS_PATH := $(GAME_PATH)/BroMaker_Storage

# Main targets
all: .install-rocketlib .install-bromaker
	$(MSBUILD) BroforceMods.sln $(MSBUILD_FLAGS)
	@$(MAKE) install-all

clean:
	$(MSBUILD) BroforceMods.sln /t:Clean $(MSBUILD_FLAGS)
	$(MSBUILD) "../RocketLib/RocketLib.sln" /t:Clean $(MSBUILD_FLAGS)
	$(MSBUILD) "../Bro-Maker/BroMakerLib.sln" /t:Clean $(MSBUILD_FLAGS)
	@rm -f .install-rocketlib .install-bromaker

rebuild:
	$(MAKE) clean
	$(MAKE) all

# Dependency build targets
.PHONY: build-rocketlib build-bromaker

build-rocketlib:
	$(MSBUILD) "../RocketLib/RocketLib.sln" $(MSBUILD_FLAGS)

build-bromaker:
	$(MSBUILD) "../Bro-Maker/BroMakerLib.sln" $(MSBUILD_FLAGS)

# Dependency installation markers
.install-rocketlib: build-rocketlib
	@mkdir -p "$(MODS_PATH)/RocketLib"
	@cp -f "../RocketLib/RocketLib/_Mod/"* "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@cp -rf "../RocketLib/RocketLib/_Mod/Resources" "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@cp -f "../RocketLib/RocketLib/bin/Release/RocketLib.dll" "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@echo "  ✓ Installed RocketLib"
	@touch $@

.install-bromaker: build-bromaker
	@mkdir -p "$(MODS_PATH)/BroMaker"
	@cp -f "../Bro-Maker/BroMakerLib/_Mod/"* "$(MODS_PATH)/BroMaker/" 2>/dev/null || true
	@cp -f "../Bro-Maker/BroMakerLib/bin/Release/BroMakerLib.dll" "$(MODS_PATH)/BroMaker/" 2>/dev/null || true
	@echo "  ✓ Installed BroMaker"
	@touch $@

# Installation functions (not targets)
define install-rocketlib-files
	@mkdir -p "$(MODS_PATH)/RocketLib"
	@cp -f "../RocketLib/RocketLib/_Mod/"* "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@cp -rf "../RocketLib/RocketLib/_Mod/Resources" "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@cp -f "../RocketLib/RocketLib/bin/Release/RocketLib.dll" "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@echo "  ✓ Installed RocketLib"
endef

define install-bromaker-files
	@mkdir -p "$(MODS_PATH)/BroMaker"
	@cp -f "../Bro-Maker/BroMakerLib/_Mod/"* "$(MODS_PATH)/BroMaker/" 2>/dev/null || true
	@cp -f "../Bro-Maker/BroMakerLib/bin/Release/BroMakerLib.dll" "$(MODS_PATH)/BroMaker/" 2>/dev/null || true
	@echo "  ✓ Installed BroMaker"
endef

# Individual project build targets
utility-mod: .install-rocketlib
	$(MSBUILD) "Utility Mod/Utility Mod.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(MODS_PATH)/Utility Mod"
	@cp -f "Utility Mod/Utility Mod/bin/Release/Utility Mod.dll" "$(MODS_PATH)/Utility Mod/"
	@echo "  ✓ Installed Utility Mod"

swap-bros: .install-bromaker
	$(MSBUILD) "Swap Bros Mod/Swap Bros Mod.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(MODS_PATH)/Swap Bros Mod"
	@cp -f "Swap Bros Mod/Swap Bros Mod/bin/Release/Swap Bros Mod.dll" "$(MODS_PATH)/Swap Bros Mod/"
	@echo "  ✓ Installed Swap Bros Mod"

captain-ameribro: .install-bromaker
	$(MSBUILD) "Captain Ameribro/Captain Ameribro Mod.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(BROS_PATH)/Captain Ameribro"
	@cp -f "Captain Ameribro/Captain Ameribro Mod/bin/Release/Captain Ameribro Mod.dll" "$(BROS_PATH)/Captain Ameribro/Captain Ameribro.dll"
	@echo "  ✓ Installed Captain Ameribro"

mission-impossibro: .install-rocketlib .install-bromaker
	$(MSBUILD) "Mission Impossibro/Mission Impossibro.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(BROS_PATH)/Mission Impossibro"
	@cp -f "Mission Impossibro/Mission Impossibro/bin/Release/Mission Impossibro.dll" "$(BROS_PATH)/Mission Impossibro/"
	@echo "  ✓ Installed Mission Impossibro"

brostbuster: .install-rocketlib .install-bromaker
	$(MSBUILD) "Brostbuster/Brostbuster.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(BROS_PATH)/Brostbuster"
	@cp -f "Brostbuster/Brostbuster/bin/Release/Brostbuster.dll" "$(BROS_PATH)/Brostbuster/"
	@echo "  ✓ Installed Brostbuster"

rjbrocready: .install-rocketlib .install-bromaker
	$(MSBUILD) "RJBrocready/RJBrocready.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(BROS_PATH)/RJBrocready"
	@cp -f "RJBrocready/RJBrocready/bin/Release/RJBrocready.dll" "$(BROS_PATH)/RJBrocready/"
	@echo "  ✓ Installed RJBrocready"

furibrosa: .install-rocketlib .install-bromaker
	$(MSBUILD) "Furibrosa/Furibrosa.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(BROS_PATH)/Furibrosa"
	@cp -f "Furibrosa/Furibrosa/bin/Release/Furibrosa.dll" "$(BROS_PATH)/Furibrosa/"
	@echo "  ✓ Installed Furibrosa"

drunken-broster: .install-rocketlib .install-bromaker
	$(MSBUILD) "Drunken Broster/Drunken Broster.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(BROS_PATH)/Drunken Broster"
	@cp -f "Drunken Broster/Drunken Broster/bin/Release/Drunken Broster.dll" "$(BROS_PATH)/Drunken Broster/"
	@echo "  ✓ Installed Drunken Broster"

control-enemies: .install-rocketlib .install-bromaker
	$(MSBUILD) "Control Enemies Mod/Control Enemies Mod.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(MODS_PATH)/Control Enemies Mod"
	@cp -f "Control Enemies Mod/Control Enemies Mod/bin/Release/Control Enemies Mod.dll" "$(MODS_PATH)/Control Enemies Mod/"
	@echo "  ✓ Installed Control Enemies Mod"

randomizer:
	$(MSBUILD) "Randomizer Mod/Randomizer Mod.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(MODS_PATH)/Randomizer Mod"
	@cp -f "Randomizer Mod/Randomizer Mod/bin/Release/Randomizer Mod.dll" "$(MODS_PATH)/Randomizer Mod/"
	@echo "  ✓ Installed Randomizer Mod"

unity-inspector:
	$(MSBUILD) "Unity Inspector Mod/Unity Inspector Mod.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(MODS_PATH)/Unity Inspector Mod"
	@cp -f "Unity Inspector Mod/Unity Inspector Mod/bin/Release/Unity Inspector Mod.dll" "$(MODS_PATH)/Unity Inspector Mod/"
	@echo "  ✓ Installed Unity Inspector Mod"

ironbro-multiplayer:
	$(MSBUILD) "IronBro Multiplayer Mod/IronBro Multiplayer Mod.sln" $(MSBUILD_FLAGS)
	@mkdir -p "$(MODS_PATH)/IronBro Multiplayer Mod"
	@cp -f "IronBro Multiplayer Mod/IronBro Multiplayer Mod/bin/Release/IronBro Multiplayer Mod.dll" "$(MODS_PATH)/IronBro Multiplayer Mod/"
	@echo "  ✓ Installed IronBro Multiplayer Mod"

# Standalone dependency targets
rocketlib: .install-rocketlib

bromaker: .install-bromaker

# Install all built DLLs to game directories
install-all:
	@mkdir -p "$(MODS_PATH)/Utility Mod" 2>/dev/null || true
	@cp -u "Utility Mod/Utility Mod/bin/Release/Utility Mod.dll" "$(MODS_PATH)/Utility Mod/" 2>/dev/null && echo "  ✓ Updated Utility Mod" || true
	@mkdir -p "$(MODS_PATH)/Swap Bros Mod" 2>/dev/null || true
	@cp -u "Swap Bros Mod/Swap Bros Mod/bin/Release/Swap Bros Mod.dll" "$(MODS_PATH)/Swap Bros Mod/" 2>/dev/null && echo "  ✓ Updated Swap Bros Mod" || true
	@mkdir -p "$(MODS_PATH)/Control Enemies Mod" 2>/dev/null || true
	@cp -u "Control Enemies Mod/Control Enemies Mod/bin/Release/Control Enemies Mod.dll" "$(MODS_PATH)/Control Enemies Mod/" 2>/dev/null && echo "  ✓ Updated Control Enemies Mod" || true
	@mkdir -p "$(MODS_PATH)/Randomizer Mod" 2>/dev/null || true
	@cp -u "Randomizer Mod/Randomizer Mod/bin/Release/Randomizer Mod.dll" "$(MODS_PATH)/Randomizer Mod/" 2>/dev/null && echo "  ✓ Updated Randomizer Mod" || true
	@mkdir -p "$(MODS_PATH)/Unity Inspector Mod" 2>/dev/null || true
	@cp -u "Unity Inspector Mod/Unity Inspector Mod/bin/Release/Unity Inspector Mod.dll" "$(MODS_PATH)/Unity Inspector Mod/" 2>/dev/null && echo "  ✓ Updated Unity Inspector Mod" || true
	@mkdir -p "$(MODS_PATH)/IronBro Multiplayer Mod" 2>/dev/null || true
	@cp -u "IronBro Multiplayer Mod/IronBro Multiplayer Mod/bin/Release/IronBro Multiplayer Mod.dll" "$(MODS_PATH)/IronBro Multiplayer Mod/" 2>/dev/null && echo "  ✓ Updated IronBro Multiplayer Mod" || true
	@mkdir -p "$(BROS_PATH)/Captain Ameribro" 2>/dev/null || true
	@cp -u "Captain Ameribro/Captain Ameribro Mod/bin/Release/Captain Ameribro Mod.dll" "$(BROS_PATH)/Captain Ameribro/Captain Ameribro.dll" 2>/dev/null && echo "  ✓ Updated Captain Ameribro" || true
	@mkdir -p "$(BROS_PATH)/Mission Impossibro" 2>/dev/null || true
	@cp -u "Mission Impossibro/Mission Impossibro/bin/Release/Mission Impossibro.dll" "$(BROS_PATH)/Mission Impossibro/" 2>/dev/null && echo "  ✓ Updated Mission Impossibro" || true
	@mkdir -p "$(BROS_PATH)/Brostbuster" 2>/dev/null || true
	@cp -u "Brostbuster/Brostbuster/bin/Release/Brostbuster.dll" "$(BROS_PATH)/Brostbuster/" 2>/dev/null && echo "  ✓ Updated Brostbuster" || true
	@mkdir -p "$(BROS_PATH)/RJBrocready" 2>/dev/null || true
	@cp -u "RJBrocready/RJBrocready/bin/Release/RJBrocready.dll" "$(BROS_PATH)/RJBrocready/" 2>/dev/null && echo "  ✓ Updated RJBrocready" || true
	@mkdir -p "$(BROS_PATH)/Furibrosa" 2>/dev/null || true
	@cp -u "Furibrosa/Furibrosa/bin/Release/Furibrosa.dll" "$(BROS_PATH)/Furibrosa/" 2>/dev/null && echo "  ✓ Updated Furibrosa" || true
	@mkdir -p "$(BROS_PATH)/Drunken Broster" 2>/dev/null || true
	@cp -u "Drunken Broster/Drunken Broster/bin/Release/Drunken Broster.dll" "$(BROS_PATH)/Drunken Broster/" 2>/dev/null && echo "  ✓ Updated Drunken Broster" || true
	@mkdir -p "$(MODS_PATH)/RocketLib" 2>/dev/null || true
	@cp -u "../RocketLib/RocketLib/_Mod/"*.json "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@cp -u "../RocketLib/RocketLib/_Mod/"*.txt "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@cp -u "../RocketLib/RocketLib/_Mod/"*.xml "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@cp -rf "../RocketLib/RocketLib/_Mod/Resources" "$(MODS_PATH)/RocketLib/" 2>/dev/null || true
	@cp -u "../RocketLib/RocketLib/bin/Release/RocketLib.dll" "$(MODS_PATH)/RocketLib/" 2>/dev/null && echo "  ✓ Updated RocketLib" || true
	@mkdir -p "$(MODS_PATH)/BroMaker" 2>/dev/null || true
	@cp -u "../Bro-Maker/BroMakerLib/_Mod/"*.json "$(MODS_PATH)/BroMaker/" 2>/dev/null || true
	@cp -u "../Bro-Maker/BroMakerLib/_Mod/"*.txt "$(MODS_PATH)/BroMaker/" 2>/dev/null || true
	@cp -u "../Bro-Maker/BroMakerLib/_Mod/"*.xml "$(MODS_PATH)/BroMaker/" 2>/dev/null || true
	@cp -u "../Bro-Maker/BroMakerLib/bin/Release/BroMakerLib.dll" "$(MODS_PATH)/BroMaker/" 2>/dev/null && echo "  ✓ Updated BroMaker" || true

# Help target
help:
	@echo "BroforceMods Build System"
	@echo ""
	@echo "Main targets:"
	@echo "  make          - Build all projects"
	@echo "  make clean    - Clean all projects"
	@echo "  make rebuild  - Clean and rebuild all"
	@echo ""
	@echo "Individual projects:"
	@echo "  make utility-mod         - Build Utility Mod"
	@echo "  make swap-bros           - Build Swap Bros Mod"
	@echo "  make captain-ameribro    - Build Captain Ameribro"
	@echo "  make mission-impossibro  - Build Mission Impossibro"
	@echo "  make brostbuster         - Build Brostbuster"
	@echo "  make rjbrocready         - Build RJBrocready"
	@echo "  make furibrosa           - Build Furibrosa"
	@echo "  make drunken-broster     - Build Drunken Broster"
	@echo "  make control-enemies     - Build Control Enemies Mod"
	@echo "  make randomizer          - Build Randomizer Mod"
	@echo "  make unity-inspector     - Build Unity Inspector Mod"
	@echo "  make ironbro-multiplayer - Build IronBro Multiplayer Mod"
	@echo ""
	@echo "Dependencies:"
	@echo "  make rocketlib - Build RocketLib"
	@echo "  make bromaker  - Build BroMakerLib"
	@echo ""
	@echo "Parallel builds:"
	@echo "  make -j8      - Build all projects in parallel"
	@echo "  make -j8 rebuild - Clean and rebuild in parallel"

.PHONY: all clean rebuild install-all build-rocketlib build-bromaker help utility-mod swap-bros captain-ameribro mission-impossibro brostbuster rjbrocready furibrosa drunken-broster control-enemies randomizer unity-inspector ironbro-multiplayer rocketlib bromaker