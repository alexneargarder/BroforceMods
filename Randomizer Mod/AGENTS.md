# Randomizer Mod

- Settings persist enabled types as indices into `Main`'s name arrays (`normalList`, `ammoList`, ...), and the patches' `switch` statements map indices to prefabs in array order. Add a type at the end of its array and to every `switch` reading it; reordering scrambles saved selections.
- `ammoList` follows the game's ammo-crate `variation` order: the chosen index is written straight into it.
- Check the commented-out arrays at the top of `Main` (types that fail to spawn or misbehave) before adding a type.
- The debug picker (`settings.DEBUG`) has its own lists and its own `switch` in `Map_PlaceDoodad`.
- With `enableInstantWin` off, `DolphLundrenSoldier_Gib`, `MinibossEndCheck_Update` and `GoliathMech_Death` cancel a boss's level win only if it carries the `RandomizedUnit` marker, so a level's own boss still ends it. Every path that places or swaps in a unit must add the marker: an unmarked randomized boss ends the level when killed.
