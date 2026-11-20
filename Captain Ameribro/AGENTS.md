# Captain Ameribro

- While a pocketed special is held the `SpecialAmmo` getter returns 1 whatever shields he holds, so code asking whether he holds a shield reads `_specialAmmo`, and every change to his shields goes through the `SpecialAmmo` setter, which swaps the sprite and gun materials.
- The charged throw runs on `animateSpecial` and `specialFrame`, not `usingSpecial`; `usingSpecial` with `grabbingShield` is the catch animation.
- A shield caught before his `Start` (`finishedStartup` false) sets `caughtShieldFromPrevious`, which `Start` credits as ammo.
