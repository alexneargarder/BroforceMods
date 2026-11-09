# Swap Bros Mod

- Utility Mod and Unity Inspector Mod call `Swap_Bros_Mod.API` by reflection, by method name; Unity Inspector Mod also reads by name `Main`'s `settings`, `allCustomBros`, `actuallyAllCustomBros`, `customBroCount` and `CreateBroList()`, and the `Settings` fields `ignoreCurrentUnlocked`, `alwaysChosen`, `selGridInt`, `enableBromaker`, `includeExpendabros`, `includeUnfinishedCharacters`, `filterBros`, `enabledBros` and `ignoreForcedBros`. Renaming any of them or changing a signature breaks those mods at run time with no compile error.
- Bros are identified by display name. `API.HeroTypeToString`/`StringToHeroType` and `Main`'s vanilla pools share the names; extend them together.
- All spawn selection is in `Player_SpawnHero_Patch`'s prefix. A custom bro spawn (`MakeCustomBroSpawn`) must set `nextHeroType` to `HeroType.Rambro`, or a leftover Boondock Bro spawns.
- During the swap cooldown `Player_Update_Patch` calls `SetInvulnerable( 0f, false )` every frame; `TestVanDammeAnim_SetInvulnerable_Patch` treats exactly that call as "clear invulnerability" without restarting the bubble.
- BroMaker is optional, yet `Main` references BroMakerLib types directly: keep every BroMaker call behind `settings.enableBromaker`, which `Load` turns off when its first BroMaker call (`LoadCustomBros()`, inside a `try`/`catch`) fails.
