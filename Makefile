# BroforceMods Makefile

PROJECT_NAME := BroforceMods
SOLUTION := BroforceMods.sln

# Custom help text for individual projects
define EXTRA_HELP
	@echo "Individual projects:"
	@echo "  make brostbuster        make mission-impossibro"
	@echo "  make captain-ameribro   make randomizer"
	@echo "  make control-enemies    make rjbrocready"
	@echo "  make drunken-broster    make swap-bros"
	@echo "  make furibrosa          make unity-inspector"
	@echo "  make ironbro-multiplayer make utility-mod"
endef
export EXTRA_HELP

include Scripts/Makefile.common

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
