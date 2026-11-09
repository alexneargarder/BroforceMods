# BroforceDevTools

- No `PatchAll`: each tool patches by hand on its own Harmony instance. `OnToggle(false)` stops the recorder, cleans every tool up and removes the Demystify patches; `OnToggle(true)` reapplies them.
- `FPSCounterAPI` and `ConstructorProfilerAPI` are the static facades other mods and `execute_code` call. The Patch Profiler and Profiling Recorder refuse to start unless the FPS Counter runs.
- `UnityEngine` shadows `System.Math`: write `System.Math.Abs`.
- This Unity's `WaitForSecondsRealtime` never resets its deadline; reuse the recorder's own wait.
- Only `StopRecording` writes the recorder's results (to `<mod folder>/Results/<timestamp>/`); a session never stopped writes nothing.
- The overlay's fixed-size string buffers return their whole capacity padded with NULs: append each by its real length (`StringOutputLength`).
- The RAM line's Windows P/Invokes fail on the game's Linux launch; the first failure stops the queries.
- The Patch Profiler's and mod counter's wrappers on other mods' patches stay installed across Stop/Start, returning early while off, and are removed only in `Cleanup`: Harmony regenerates a method's wrapper on every unpatch, costly over hundreds of patches.

## Constructor Profiler

- Patch only constructors with an IL body in `Assembly-CSharp`, `Assembly-CSharp-firstpass` and loaded mods' assemblies, never delegate, runtime or internal-call ones. Patching framework constructors mid-game crashes; patching a delegate constructor empties every delegate created after.
- `AddCall` stringifies each stack trace at once; reading a stored `StackTrace` later crashes natively.
- Constructors run on any thread: the recording flag is `volatile` and the call store is read and swapped under its lock.

## Demystify

- Never read the IL of a method from a Reflection.Emit module (`DynamicMethod`, `ModuleBuilder`/`AssemblyBuilder`, or an assembly with no file `Location`): Mono aborts natively in `GetMethodBody`.
- Every file in `Demystify/` except `DemystifyPatcher` and `InteropHelper` is Ben.Demystifier code (Apache 2.0); the BepInEx.Debug (LGPL-3.0) and FPSCounter (Apache 2.0) ports are also third-party. A modified ported file carries the header `// Modified from the original; see THIRD-PARTY-NOTICES.md.` and is listed with its change in `_ModContent/THIRD-PARTY-NOTICES.md`.
- Ported files are in the root `Makefile`'s `CLEANUP_EXCLUDE`; add a newly ported file there.
