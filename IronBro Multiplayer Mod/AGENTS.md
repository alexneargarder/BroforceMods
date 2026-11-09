# IronBro Multiplayer Mod

- To lift an Iron Bro restriction around one vanilla call, set `GameModeController_IsHardcoreMode_Patch.disableHardcoreCheck` in that call's prefix and clear it in its postfix; while set, `IsHardcoreMode` reports `false`.
- Utility Mod has a copy of this helicopter wait (`helicopterWait`) on the same game methods, and with both mods' waits on, each mod's level-finish calls run the other's patches: change the two together.
