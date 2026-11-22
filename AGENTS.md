# BroforceMods

Broforce mods, one project each: Unity Mod Manager (UMM) mods and BroMaker custom bros. Most use [RocketLib](https://github.com/Gorzontrok/RocketLib); the bros use [Bro-Maker](https://github.com/Gorzontrok/Bro-Maker), whose `AGENTS.md` files cover the library side.

Setup, Unity Mod Manager, logs and debugging, multiplayer testing and Thunderstore releases are documented only in the [BroforceMods wiki](https://github.com/alexneargarder/BroforceMods/wiki); clone `https://github.com/alexneargarder/BroforceMods.wiki.git` to search it locally.

## Build

- Each project is `<Name>/<Name>/<Name>.csproj` with its own `.sln`. Run `make -C .. <target>` from `<Name>/`.
- After changing RocketLib or BroMakerLib, rebuild the library with `make -C ../RocketLib build` or `make -C ../Bro-Maker build` from this repo's root, with the clones side by side: mods compile against its installed build.
- `Scripts/BroforceModBuild.targets`, `Scripts/Makefile.common`, `Scripts/BroforceMods.DotSettings`, the root `.editorconfig` and `LocalBroforcePath.example.props` are edited in [Broforce-Templates](https://github.com/alexneargarder/Broforce-Templates) and synced here, never edited in this repo; their facts are in [its `Scripts/AGENTS.md`](https://github.com/alexneargarder/Broforce-Templates/blob/master/Scripts/AGENTS.md).
- Projects target .NET Framework 3.5 (the game's Mono): no `Task`/`async`, `Tuple`, `ConcurrentDictionary`, `string.IsNullOrWhiteSpace`, `Enum.HasFlag`, `IReadOnly*` or `Dictionary.GetValueOrDefault`. Use `Thread`/`ThreadPool` or coroutines.
- Namespace: the project folder's name with spaces turned to underscores; subfolders add sub-namespaces.
- Install a mod only through its build, never by copying a DLL by hand into `_ModContent/` or the game; never commit a built DLL.

## UMM mods and bros

- A UMM mod (`_ModContent/Info.json`) other than Unity Inspector Mod and BroforceDevTools: static `Main.Load` keeps the entry in `Main.mod` and calls `PatchAll` once; `OnToggle` stores `Main.enabled` and returns `true`, and patch bodies return early while it is false.
- A BroMaker bro (`_ModContent/<Name>.mod.json`): a `[HeroPreset]` class deriving from `CustomHero` or a BroMakerLib subclass; BroMaker calls its `HarmonyPatches( Harmony )` override, so it never creates its own `Harmony`. Handle situations the base game handles for its bros the way the vanilla bros do, and expose behavior players may want in custom levels as a RocketLib `CustomTriggerAction<T>` registered in `RegisterCustomTriggers()`.
- Outside Unity Inspector Mod and BroforceDevTools, patches are attribute-declared and installed by the one `PatchAll` call (a bro's in its `HarmonyPatches` override), never by hand. A patch class is `static`, named `<Type>_<Method>_Patch`, usually nested in a `HarmonyPatches` class.
- UMM mod settings are UMM XML (`UnityModManager.ModSettings` or RocketLib's `XmlModSettings`): no interfaces or `Dictionary<,>`; persist a dictionary as an `[XmlIgnore]` field plus an `[XmlArray]` property of RocketLib's `SerializableKeyValuePair` array. A bro persists settings through BroMaker's `[SaveableSetting]` static fields.
- A mod that uses another mod only when it's installed (BroMaker, Swap Bros Mod) reaches it by reflection or only from methods called inside a `try`/`catch`: a direct call into a missing assembly breaks the mod for players without it.

## Logging

- Never call `UnityEngine.Debug.Log`: its output never reaches the Unity Mod Manager (UMM) log.
- Log through `Main.mod.Logger.Log(...)` in a UMM mod, `this.Log(...)` (BroMakerLib's `ICustomHero` extension) in a BroMaker bro's hero class and `BMLogger.Log(...)` in its other classes.
- For file logging, extend RocketLib's logger; never write ad-hoc file output in a mod.

## Logs

- **UMM log** (UMM's and every mod logger's output): `UMM/Core/Log.txt` in the r2modman profile the game ran with, under `%AppData%\r2modmanPlus-local\Broforce\profiles\<profile>\` on Windows and `~/.config/r2modmanPlus-local/Broforce/profiles/<profile>/` on Linux.
- **Unity player log** (the engine's log from process start, overwritten each launch): read it when the UMM log is missing, for engine errors and for native crashes. Windows `%USERPROFILE%\AppData\LocalLow\Free Lives\Broforce\output_log.txt`; native Linux `~/.config/unity3d/Free Lives/Broforce/Player.log`; Proton `<Steam library>/steamapps/compatdata/274190/pfx/drive_c/users/steamuser/AppData/LocalLow/Free Lives/Broforce/output_log.txt`.

## In-game testing

- Before reporting a game run, account for every crash, error, exception and BroMaker Issues entry its log holds: trace it to its cause in source, or show it in a vanilla or default-branch run of the same steps.
- Never crash the game on purpose, or drive it with a load normal play doesn't produce.
- A change a player meets in game is done once the scenario scripts [unity-inspector-mcp's `scripts/README.md`](https://github.com/alexneargarder/unity-inspector-mcp/blob/master/scripts/README.md) names for it pass on the final build, with every new UMM log line fixed or explained.

## Code style

- Comment only a genuinely complex algorithm or an engine or runtime gotcha the code can't show: at most two lines, at the point of use, never wrapped to a column width. No labels, rationale, ordering or coupling notes, contracts, restatements, deletions or history; keep existing label comments.
- No doc blocks on files, classes, methods or fields, except a one-sentence XML `///` comment on a public API another project calls.
- Run `make cleanup-changed` (ReSharper cleanup of changed C# lines) before every commit, never during feature work; `BASE=master` adds the branch's committed changes.
- A project whose history has no commit reformatting the whole project is formatted only by its own pull request running `make cleanup PROJECT=<name>` and carrying no other change; until that merges, skip `make cleanup-changed` in it.

## Commits

- A commit is one imperative subject line with no body, naming at most its two largest changes, with no trailing period and no version number except in a `Release vX.Y.Z` commit.
- A commit changing one project starts `<ProjectName> - `, its release commit included (`<ProjectName> - Release vX.Y.Z`); one changing several projects or only repo-level files has no prefix.

## Changelogs

- `Releases/<ProjectName>/Changelog.md`. Headers are `## vX.Y.Z`, newest first; `(unreleased)` on the topmost marks a version not yet packaged. Keep that header at the version being worked on: `bt package` packages the highest of it, `manifest.json`'s `version_number` and `Info.json`/`.mod.json`'s `Version`.
- Entries are `-` bullets, each a change a player would notice in one short, non-technical clause of at most 20 words, never a list of symptoms, names or implementation detail. A branch adds at most one bug-fix line: one clause saying what its fixes change for a player.
- Write a branch's lines once its changes are complete, covering the whole branch.
- Versioning: `v1.0.X` for minor features, non-breaking changes and small fixes; `v1.X.0` for breaking changes, significant features and BroMaker compatibility updates; `vX.0.0` for major rewrites or major new features.

## Docs

- Repo docs (`AGENTS.md` files, READMEs, tool descriptions, changelogs) state the current contract only: no other project's defects, transitional caveats or fixes in progress.

## Releases

- Players install mods from Thunderstore through r2modman, which pulls in Unity Mod Manager as a dependency of every mod package: never point install instructions at a Nexus Mods UMM page or a manual UMM install.
- `bt package` builds the Thunderstore zip.
- Mod READMEs and Thunderstore pages link the end-user setup guide: https://steamcommunity.com/sharedfiles/filedetails/?id=2434812447

## Folder AGENTS.md files

- `BroforceDevTools/AGENTS.md` — manual patching, profiler wrapper lifetime and crash-avoiding scope, Demystify limits, ported-code license duties
- `Brostbuster/AGENTS.md` — trapped-unit patch guard, Slimer blood-color counter
- `Captain Ameribro/AGENTS.md` — shield as special ammo, throw and catch state
- `Control Enemies Mod/AGENTS.md` — unit-name markers, optional BroMaker calls, null mook spawns
- `Drunken Broster/AGENTS.md` — melee-item enum and additions, friendly-spawn marker, drunk camera
- `Furibrosa/AGENTS.md` — grabbed-unit patch guard and release, War Rig's duplicated weapon code
- `IronBro Multiplayer Mod/AGENTS.md` — hardcore-check suppression flag, helicopter wait shared with Utility Mod
- `Mission Impossibro/AGENTS.md` — tranq-dart boss stun counter
- `Randomizer Mod/AGENTS.md` — index-keyed type pools, randomized-boss marker
- `RJBrocready/AGENTS.md` — Thing state ordering, IronBro carryover
- `Swap Bros Mod/AGENTS.md` — names other mods read by reflection, custom-bro spawning, swap cooldown, optional BroMaker
- `Unity Inspector Mod/AGENTS.md` — the game side of unity-inspector-mcp: server, main-thread dispatch, command handlers, input simulation, the script compiler and `execute_code`
- `Utility Mod/AGENTS.md` — adding context-menu actions and keybindings, helicopter wait shared with IronBro, enemy-spawn suppression, pause state
