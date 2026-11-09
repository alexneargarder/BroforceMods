# Drunken Broster

- `DrunkenBroster.MeleeItem` values index the sprite sheets (melee-sheet row `(int)item + 1`, two gun-sheet rows per value): keep the enum in sheet order, `None` last.
- Adding a melee item touches the enum, the default `EnabledMeleeItems` and its copy behind the "Reset to default items" button, `ChooseItem`'s weighted pool, the per-item melee hit, `ThrowHeldItem`, its prefab, and the "Set Melee Item Pool" trigger's toggles.
- Friendly spawns are base-game hostiles marked `playerNum = 5` and `firingPlayerNum = 5`, with patches making the vanilla AI cooperate; use the same marker for a new one.
- `SkullProjectile` is a `MonoBehaviour` on a live `HellLostSoul`, not a projectile.
- `DrunkenCameraManager` is static and shared: a new way into or out of the drunk state must register or unregister the bro with it.
