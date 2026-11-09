# Utility Mod

- Adding a context-menu action touches the `MenuActionType` value, a `Create...` factory on `MenuAction`, its case in `MenuAction.Execute()`, and the item in `ContextMenuBuilder`; a new spawnable also needs a `SpawnableTypes` value. One that should replay as a level edit needs its case in `ContextMenuUI.TrackLevelEdit`, called only once the action has succeeded (a failed spawn is no level edit), and paint mode repeats it only if `ContextMenuModes.IsPaintEligible()` accepts it (keep one-shot actions out).
- Adding a keybinding: its name in `Main`'s `keybindingActions` (a name missing there is never loaded) and its case in `ExecuteAction()`. Only bound keys are polled, and only player 0's.
- IronBro Multiplayer Mod has a copy of Helicopter Wait (`helicopterWait`) on the same game methods, and with both mods' waits on, each mod's level-finish calls run the other's patches: change the two together.
- Disable Enemy Spawns must keep whatever a level needs to finish: never suppress `AlienBoss` or `HellBoss` doodads, or a boss level can't end. Its skipped `MapController.SpawnMook_Networked`/`SpawnMook_Local` return null to the game code that called them.
- Paused is `Time.timeScale == 0` (`Main.TimeIsStopped`), never a stored flag: a level load resets the scale to 1.
