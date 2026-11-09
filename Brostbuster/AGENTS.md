# Brostbuster

- His Harmony prefixes skip the original for units in `GhostTrap.grabbedUnits`, so a trapped unit runs none of its own physics or animation. Guard any new trap-related patch the same way.
- `Slimer.overrideBloodColor` is a counter: increment it around a Slimer effect path and decrement on the way out; above zero, green blood turns `Brostbuster.SlimerColor`.
