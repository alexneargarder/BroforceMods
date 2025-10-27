# BroforceMods Makefile
# Minimal MSBuild wrapper - leverages Scripts/BroforceModBuild.targets for installation

MSBUILD := /mnt/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe
MSBUILD_FLAGS := /p:Configuration=Release /verbosity:minimal /nologo

# LAUNCH variable controls both kill and launch behavior
# Usage: make furibrosa LAUNCH=no
ifeq ($(LAUNCH),no)
	LAUNCH_FLAGS := /p:CloseBroforceOnBuild=false /p:LaunchBroforceOnBuild=false
else
	LAUNCH_FLAGS := /p:CloseBroforceOnBuild=true /p:LaunchBroforceOnBuild=true
endif

# Default target shows help
.DEFAULT_GOAL := help

.PHONY: help
help:
	@echo "BroforceMods Build System"
	@echo ""
	@echo "Main targets:"
	@echo "  make build              Build all projects (kill game, build, launch)"
	@echo "  make build-no-launch    Build all without disrupting running game"
	@echo "  make clean              Clean all projects"
	@echo "  make rebuild            Clean and rebuild all"
	@echo ""
	@echo "Individual projects:"
	@echo "  make brostbuster        make mission-impossibro"
	@echo "  make captain-ameribro   make randomizer"
	@echo "  make control-enemies    make rjbrocready"
	@echo "  make drunken-broster    make swap-bros"
	@echo "  make furibrosa          make unity-inspector"
	@echo "  make ironbro-multiplayer make utility-mod"
	@echo ""
	@echo "Options:"
	@echo "  LAUNCH=no               Don't kill or launch game"
	@echo ""
	@echo "Examples:"
	@echo "  make furibrosa              Kill game, build, launch"
	@echo "  make furibrosa LAUNCH=no    Build without disrupting running game"
	@echo "  make build-no-launch        Build all without disrupting game"

.PHONY: build
build:
	"$(MSBUILD)" BroforceMods.sln $(MSBUILD_FLAGS) /p:CloseBroforceOnBuild=true /p:LaunchBroforceOnBuild=true

.PHONY: build-no-launch
build-no-launch:
	"$(MSBUILD)" BroforceMods.sln $(MSBUILD_FLAGS) /p:CloseBroforceOnBuild=false /p:LaunchBroforceOnBuild=false

.PHONY: clean
clean:
	"$(MSBUILD)" BroforceMods.sln /t:Clean $(MSBUILD_FLAGS)

.PHONY: rebuild
rebuild: clean
	"$(MSBUILD)" BroforceMods.sln $(MSBUILD_FLAGS) /p:CloseBroforceOnBuild=false /p:LaunchBroforceOnBuild=false

# Individual project targets
.PHONY: brostbuster
brostbuster:
	"$(MSBUILD)" "Brostbuster/Brostbuster/Brostbuster.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: captain-ameribro
captain-ameribro:
	"$(MSBUILD)" "Captain Ameribro/Captain Ameribro/Captain Ameribro.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: control-enemies
control-enemies:
	"$(MSBUILD)" "Control Enemies Mod/Control Enemies Mod/Control Enemies Mod.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: drunken-broster
drunken-broster:
	"$(MSBUILD)" "Drunken Broster/Drunken Broster/Drunken Broster.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: furibrosa
furibrosa:
	"$(MSBUILD)" "Furibrosa/Furibrosa/Furibrosa.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: ironbro-multiplayer
ironbro-multiplayer:
	"$(MSBUILD)" "IronBro Multiplayer Mod/IronBro Multiplayer Mod/IronBro Multiplayer Mod.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: mission-impossibro
mission-impossibro:
	"$(MSBUILD)" "Mission Impossibro/Mission Impossibro/Mission Impossibro.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: randomizer
randomizer:
	"$(MSBUILD)" "Randomizer Mod/Randomizer Mod/Randomizer Mod.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: rjbrocready
rjbrocready:
	"$(MSBUILD)" "RJBrocready/RJBrocready/RJBrocready.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: swap-bros
swap-bros:
	"$(MSBUILD)" "Swap Bros Mod/Swap Bros Mod/Swap Bros Mod.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: unity-inspector
unity-inspector:
	"$(MSBUILD)" "Unity Inspector Mod/Unity Inspector Mod/Unity Inspector Mod.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)

.PHONY: utility-mod
utility-mod:
	"$(MSBUILD)" "Utility Mod/Utility Mod/Utility Mod.csproj" $(MSBUILD_FLAGS) $(LAUNCH_FLAGS)
