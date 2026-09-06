# Third-Party Notices

BroforceDevTools as a whole is distributed under the GNU General Public License,
version 3, the full text of which is in `LICENSE-GPL-3.0.txt`. The complete
corresponding source for BroforceDevTools, including all of the ported code described
below, is available at https://github.com/alexneargarder/BroforceMods. It contains code
derived from the projects listed below. None of these projects ship a NOTICE file, so
there is no upstream NOTICE content to reproduce here.

## BepInEx.Debug

- Upstream: https://github.com/BepInEx/BepInEx.Debug
- Copyright: the repository names no individual copyright holder. It is the work of the
  BepInEx project and its contributors.
- License: GNU Lesser General Public License, version 3, in `LICENSE-LGPL-3.0.txt`. The
  LGPL applies the terms of the GNU General Public License, version 3, which is in
  `LICENSE-GPL-3.0.txt`.
- Files in this mod: `Demystify/DemystifyPatcher.cs`, `Demystify/InteropHelper.cs`,
  `ConstructorProfiler/ConstructorProfiler.cs`.
- Modifications: `DemystifyPatcher.cs` and `ConstructorProfiler.cs` were modified in
  September 2026, ported from BepInEx preloader patcher and plugin entry points to Unity
  Mod Manager, with the BepInEx logging, configuration and plugin metadata replaced by
  the equivalents this mod uses. `InteropHelper.cs` is unchanged.

`DemystifyPatcher.cs`, `InteropHelper.cs` and `ConstructorProfiler.cs` remain licensed
under the LGPL, version 3.

## Ben.Demystifier

- Upstream: https://github.com/benaadams/Ben.Demystifier
- Copyright: "Copyright (c) Ben A Adams. All rights reserved." Several files carry the
  notice "Copyright (c) .NET Foundation. All rights reserved." in addition or instead.
- License: Apache License, Version 2.0, in `LICENSE-Apache-2.0.txt`.
- Files in this mod: everything in `Demystify/` other than `DemystifyPatcher.cs` and
  `InteropHelper.cs`, including the `Demystify/Enumerable/` and `Demystify/Internal/`
  directories.
- Modifications: these files were taken from the vendored copy inside BepInEx.Debug's
  DemystifyExceptions, which had already adapted them to the older runtime that BepInEx
  targets. Four files of that copy were changed further here.
  `Demystify/Internal/ReflectionHelper.cs`: a null-coalescing assignment was rewritten
  for C# 7.3. `Demystify/EnhancedStackTrace.Frames.cs`: frames from `Reflection.Emit`
  methods and frames with no method are rendered without IL inspection, because the
  game's Mono runtime aborts on it. `Demystify/StringBuilderExtentions.cs`: the exception
  type and message are written before the trace so a failed trace never yields empty
  output, trace resolution is guarded so a failure writes a `[stack trace unavailable]`
  marker and keeps the rest of the output, and the `AggregateException` lookup is
  null-guarded because that type is absent from the game's runtime. `Demystify/ExceptionExtentions.cs`: the same null guard.

The headers on these files point at a `License.txt` in their original project root. The
file that carries those terms here is `LICENSE-Apache-2.0.txt`.

## FPSCounter

- Upstream: https://github.com/ManlyMarco/FPSCounter
- Copyright: ManlyMarco, stated upstream as "Copyright Apache-2.0 © 2018".
- License: Apache License, Version 2.0, in `LICENSE-Apache-2.0.txt`.
- Files in this mod: `FPSCounter/CounterColors.cs`, `FPSCounter/FixedString.cs`,
  `FPSCounter/FrameCounter.cs`, `FPSCounter/HarmonyPatchProfiler.cs`,
  `FPSCounter/MemoryInfo.cs`, `FPSCounter/ModCounter.cs`, `FPSCounter/MovingAverage.cs`,
  `FPSCounter/ShadowAndOutline.cs`.
- Modifications: all of these were ported from a BepInEx plugin to Unity Mod Manager and
  moved into this mod's namespaces. `FrameCounter.cs` is a rework of the upstream
  `FPSCounter.cs` counter and its display, `ModCounter.cs` is a rewrite of the upstream
  `PluginCounter.cs` that measures Unity Mod Manager mods where the upstream measured BepInEx plugins,
  `HarmonyPatchProfiler.cs` is new code for this mod whose per-frame collection loop
  derives from the one in `PluginCounter.cs`, the numeric formatting in `FixedString.cs`
  derives from the Gavin Pugh files described below, and the remaining files carry small
  edits only.

## StringBuilder extension methods by Gavin Pugh

- Upstream: reached this mod through FPSCounter, which vendors these files.
- Copyright: "Copyright (c) Gavin Pugh 2010", as stated in the file headers.
- License: the zlib license, http://www.opensource.org/licenses/zlib-license.php, whose
  full text is reproduced below.
- Files in this mod: `FPSCounter/StringBuilderFormat.cs`,
  `FPSCounter/StringBuilderNumerics.cs`, `FPSCounter/FixedString.cs`.
- Modifications: all three files in this mod are altered versions of the originals.
  `StringBuilderFormat.cs` and `StringBuilderNumerics.cs` were moved into this mod's
  namespace and a few unqualified `Math` calls were fully qualified. `FixedString.cs` is
  a restructuring of the same numeric-conversion code into a fixed-capacity string type,
  and reached this mod through FPSCounter, which had already dropped the Gavin Pugh
  notice from it.

```
This software is provided 'as-is', without any express or implied warranty. In no event
will the authors be held liable for any damages arising from the use of this software.

Permission is granted to anyone to use this software for any purpose, including
commercial applications, and to alter it and redistribute it freely, subject to the
following restrictions:

1. The origin of this software must not be misrepresented; you must not claim that you
   wrote the original software. If you use this software in a product, an acknowledgment
   in the product documentation would be appreciated but is not required.

2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.

3. This notice may not be removed or altered from any source distribution.
```

## Original code

`Main.cs`, `FPSCounter/FPSCounterAPI.cs`, `FPSCounter/ProfilingRecorder.cs` and
`Properties/AssemblyInfo.cs` are not derived from any of the projects above.
