# Control Enemies Mod

- Patches test unit names as markers: a possessed unit is renamed `"c"`, one mid-possession `"p"`.
- BroMaker is optional, yet `Main` references BroMakerLib types directly. Put each BroMaker access in a `Try...` method called only from a wrapper that checks `isBroMakerInstalled` and catches, clearing the flag on failure.
- `MapController.SpawnMook_Networked` returns null for a spawn Utility Mod's Disable Enemy Spawns skips: null-check its result.
