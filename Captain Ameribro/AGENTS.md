# Captain Ameribro

- `SpecialAmmo` is the shield (1 held, 0 thrown). Change possession through its setter, which swaps the sprite and gun materials, never through `_specialAmmo`.
- The charged throw runs on `animateSpecial` and `specialFrame`, not `usingSpecial`; `usingSpecial` with `grabbingShield` is the catch animation.
- A shield caught before his `Start` (`finishedStartup` false) sets `caughtShieldFromPrevious`, which `Start` credits as ammo.
