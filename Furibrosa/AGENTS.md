# Furibrosa

- Every Harmony patch is a prefix skipping the original for units in the static `Furibrosa.grabbedUnits`, so a held unit runs none of its own physics or animation. Guard any new grab-related patch the same way.
- `ReleaseUnit` is the only call that removes a live unit from `grabbedUnits`: a new way of losing the held unit must call it.
- `WarRig` carries its own copy of her weapon firing and switching code: a change to her weapons needs the matching change there.
