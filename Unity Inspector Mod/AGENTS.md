# Unity Inspector Mod

The game-side half of [unity-inspector-mcp](https://github.com/alexneargarder/unity-inspector-mcp): a TCP server (port setting `serverPort`) the Node.js MCP server connects to. The script-author contract is in [unity-inspector-mcp's root `AGENTS.md`](https://github.com/alexneargarder/unity-inspector-mcp/blob/master/AGENTS.md), which also indexes the per-tool reply contracts.

Besides `PatchAll` the mod patches by hand (`FrozenClock`, the binding patch, `SpawnTrace`, the compiler patches), and `OnToggle` starts and stops the server instead of setting `enabled`.

## Server

- UMM activates a loaded mod through `OnToggle(true)`, which starts the server when `autoStartServer` is on; `Load` starts nothing.
- A connection is served one request per line, each read, run and answered in turn, after a token hello.
- The hello's 5s deadline is a `System.Threading.Timer` that shuts the socket down: `ReceiveTimeout` is no deadline on this Mono.
- Replies go out ASCII-only (`AsciiOnly`): Mono's `StreamWriter` encodes each 1024-character block separately and would split a surrogate pair.
- Timed-out and cancelled commands drop their socket before the reply, so a write to a gone client and a client-reset read end the connection with nothing logged; a receive, hello-read or reply failure logs only while `isConnected` is set.
- The token rotates on every `TcpServer.Start`. `Start` binds, writes `unity-inspector-<port>.token`, then listens, all under `StartLock` on a `.lock` file beside it, since Mono binds with `SO_REUSEADDR` and two games could both bind; a failed bind writes nothing, and `Stop` deletes only a file its server wrote.
- The token file is 0600 from creation: a 077 `umask` on Unix and macOS; under wine, `CreateFileW` with descriptor `O:OWD:P(A;;FRFW;;;OW)`, which wine maps to 0600.

## Main thread

- `MainThreadDispatcher`'s GameObject is tagged `Unpausable`: the game's pause deactivates every other object, and its `Update` alone drains the queue. Time everything with `Stopwatch`.
- An `EnqueueAndWait` that timed out means the action did not run and never will; an action that started is waited out one more budget, then throws `MainThreadActionRunningException`.
- A stop requested by `EnqueueAndWaitStoppable` lands at `StopPoint()`, which compiled user code calls before every backward branch and at every method entry. It aborts the main thread from the main thread itself and `RunStoppable` resets the abort; never abort the main thread from another thread.
- UMM's `Logger` is unsynchronized: log through `Main.Log`, which queues off-main-thread lines for `MainThreadDispatcher.Update` to drain in order.

## Adding a command

1. Write `private object HandleFoo(JObject parameters)` and register it in `MessageHandler`'s constructor (`["foo"] = HandleFoo`); one needing the connection goes in `clientHandlers` and takes `(JObject parameters, object client)`.
2. Read numbers and booleans with `ReadInt`, `ReadLong`, `ReadFloat` and `ReadBool`, never `Value<T>()` or `ToObject<T>()`. Refuse a bad request by throwing `CallerErrorException`, which `HandleMessage` answers with its message and doesn't log; any other exception is logged and answered `Internal error`. A handler's own catch-all rethrows `ParamException` ahead of it.
3. Return anonymous objects; a failure is `success = false` with `error`.
4. Touch Unity APIs only inside `MainThreadDispatcher.EnqueueAndWait()`, and write a catch-all as `catch ( Exception ex ) when ( !MainThreadDispatcher.IsStall( ex ) )` so `HandleMessage` answers stalls uniformly; `game_state` alone answers a stall itself as a non-error `{mainThreadAnswered: false, error}`, since it is the probe callers learn of a stall from.
5. Add the matching MCP tool on the server side.

## Inspection and state

- Inspection handlers run their whole body on the main thread (`MessageHandler.OnMainThread`), since the game creates and destroys objects there.
- The walk never runs a static property's getter (it can create objects or run a static constructor) or reads a property with a `shared*` twin (`material`, `mesh`), whose getter instantiates a copy, and it skips a property marked `[Obsolete(…, true)]`, whose getter can log an engine error on every read.
- `GameObjectInspector.Resolve` scans every `Transform` because a GameObject name may hold `/`.
- Don't read `HeroController.BrosReleased`: it stays false all level when a mod spawns bros without the transport.
- `FrozenClock` patches game methods that divide unguarded by `Time.timeScale`, `Time.deltaTime` or a unit's `t` to divide by infinity instead of 0; a method whose divisions no longer number as expected is left alone and logged.
- `SpawnTrace` uses its own `Harmony` instance and prefixes without `[HarmonyPatch]`, removing exactly what it installed. On disconnect it queues the unpatch to the main thread: Harmony never runs on a receive thread.

## Simulated input

- Plans are validated on the receive thread and run by the `Unpausable` `InputDriver` from `Update`, so key state changes only on the main thread. `cancel_input_plan` answers on the receive thread, so it works with the main thread hung.
- Each `InputReader` postfix in `HarmonyPatches` returns early on its original's gate and resolves the player's controller from `HeroController.playerControllerIDs` on every read.
- The `GetMenuInputCombined` postfix counts a player's menu keys on the controller the `GetMenuInput` postfix answers them on, a keyboard player's on the stand-in gamepad, and skips any whose `ignoreFlags` bit is set. A keyboard-owned menu (`Menu.CheckInput` passes 240, every gamepad) therefore takes a keyboard player's keys only through the original's own `GetInput` reads of the keyboard controllers: directions, `fire` and `special`, never `jump` or `cancel`.
- The `binding` press postfixes `KeyBinding.IsDown` to match the pressed instance by reference, since `KeyBinding.Equals` compares keys and unbound bindings are equal. `GetAxis` never sees the press.
- `escape` is the keyboard's Escape key, answered in `Input.GetKeyDown`, `GetKey` and `GetKeyUp` for every player alike, so the game itself reports it as keyboard controller 0 and opens the pause; `cancel` is a controller's cancel press. `GetKeyUp` answers on the frame after the press ends, so a plan ending in `escape` finishes a frame later.
- A `tracks` call is one parent `Plan` holding a member plan per track, queued together so one `Tick` starts them all; only the parent is in `plans`.
- A gameplay or join press for a slot nobody plays comes from a stand-in gamepad controller, and while a local player plays only on a join screen, since in a level `HeroController.MonitorPlayerDropin` reads the same press and would drop a player in. A slot another machine plays gets no gameplay or join press.

## Script compiler

`ScriptCompiler` compiles `execute_script` scripts through mcs's `CSharpParser` → `ModuleContainer` → `AssemblyDefinitionDynamic` pipeline; `CodeExecutor` runs `execute_code` on `Mono.CSharp.Evaluator`. dnlib is UMM's copy in `UMM/Core` at run time. Every script command and `execute_code` call is one main-thread action, which serializes the compile, mcs statics and Harmony patching.

Each compiler patch below guards against a Mono or mcs failure; removing one brings it back:

- `PatchNestedTypeImport`: mcs double-inflates a nested generic's declaring type, so `foreach` over `Dictionary.Keys`/`.Values` or a `SortedDictionary` fails to compile.
- `BindGenericMethod`: binding a builder's generic instance copies its body with branches unpatched and drops its ILGenerator.
- `PatchScriptMethodBodyGuard`: makes `GetMethodBody` throw for `AssemblyBuilder` methods, refusing Harmony patches of script code; if its install fails, input naming Harmony or method bodies is refused.
- `PatchMethodEndPad`: a method ending in a try with an unreachable end would branch past its last byte and crash the JIT.
- `PatchLoopStopPoints` and `MethodEntry` (an `ILGenerator` constructor postfix) emit the stop points; `MethodEntry` also throws `RunawayRecursionException` past 512KB of stack, and `SkipHandler` skips deep finally and filter handlers after a grace period or unwind allowance, since Mono runs them on top of the throw's stack.
- `ContainerType`, `RecordForeignGenericField`, `RefuseDynamicMethodGenericField`: generic classes; a foreign-module or DynamicMethod field reference to one asserts in the JIT.
- `DropEndfilterWrite`: `BeginCatchBlock(null)` already writes `endfilter`.
- `FunctionPointerSignatures` and the reflection guard: this Mono's type cache aborts the second time a function-pointer `Type` is requested, so flagged members are read from their assembly file with dnlib and every reflection read that would request such a `Type` is refused, `ToString` included.
- `PatchPropertyDefine`: mcs calls a `DefineProperty` overload this mscorlib lacks, so every property, anonymous type and iterator fails without it.
- `PatchGuardedReflection`: a reflection read on a member whose signature names an absent assembly would crash the game; script calls get a probe that throws `FileNotFoundException` first.
- `LoadMembers` drops members whose signature names an absent assembly or holds a function pointer.
- `PatchForeignBuilderTokens`: Mono tokens another module's `FieldBuilder`/`TypeBuilder` as the emitting module's, so naming an earlier `execute_code` call's static gives Invalid IL on a read and a GC crash after a write.
- `TypeResolution`: mcs skips another assembly's top-level internal types, so naming one fails with CS0122. A name resolves to one only when it finds nothing public: a qualified name in its namespace, a simple name on a second lookup after the enclosing namespaces and the usings found nothing, so a public type keeps winning. On that second lookup two internal types the usings supply are a CS0104 ambiguity, an alias or `using static` defined during a lookup sees internal types, and the accessibility check treats another assembly's internal type as the compile's own. Its lookup patches go in all together or not at all.
- `TypeResolution`'s `Is` patches: an `is` operand that resolves neither as a type nor as a pattern loses both errors and goes on with a null or error operand (CS0584 or CS0150); they report the type's own error, or the member's when the operand's left side is a type.
- `TypeResolution`'s pattern-helper patches: the Evaluator prepares the helper a number or enum constant pattern creates a second time, and its module keeps the helper past the compile that built it, so `x is 3` on an object fails as an internal error without them.

`DetectEnumUsageError` rejects, before compiling, a reference to an enum defined in the same script, another loaded script or an earlier `execute_code` call.

Script teardown unpatches per method by owner, never `UnpatchAll`, which reads the body of every method any mod patched. It uses `DestroyImmediate` in `Unload()`'s frame so nothing of the script runs after it.

## execute_code

- Each call compiles as a bare `{ ... }` block returning through `CodeExecutor.__Set`, not mcs's `$retval`: mcs 6.13 fails to emit a top-level lambda whose local a nested lambda captures.
- Readings are tried in order (expression, statements with value, statements, declarations), each its own `Evaluator.Compile`; only the first that compiles is invoked, so no side effect runs twice. `__At(k)` markers locate a failing statement, since this Mono reports no IL offsets.
- `as declarations` is offered only when the input starts with `using`, a type-declaration keyword or modifier, or `[`: an unwrapped fallback would hoist an ordinary statement's locals into the session.
- A compiler internal failure tears the Evaluator down; it is rebuilt then, after a declarations compile that declared a generic method or class or was refused, and whenever a script assembly unregisters, re-importing `LatestEvalTypes()` and replaying the session's `using`s.
- The whole call is one `EnqueueAndWaitStoppable` action, given up before the MCP server's `execute_code` timeout so the mod reports first.
