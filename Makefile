# BroforceMods Makefile

PROJECT_NAME := BroforceMods
AUTO_DISCOVER := 1
EXCLUDE := ExampleMod
CLEANUP_EXCLUDE := BroforceDevTools/Demystify/**;BroforceDevTools/ConstructorProfiler/**;\
    BroforceDevTools/FPSCounter/CounterColors.cs;BroforceDevTools/FPSCounter/FixedString.cs;\
    BroforceDevTools/FPSCounter/FrameCounter.cs;BroforceDevTools/FPSCounter/HarmonyPatchProfiler.cs;\
    BroforceDevTools/FPSCounter/MemoryInfo.cs;BroforceDevTools/FPSCounter/ModCounter.cs;\
    BroforceDevTools/FPSCounter/MovingAverage.cs;BroforceDevTools/FPSCounter/ShadowAndOutline.cs;\
    BroforceDevTools/FPSCounter/StringBuilderFormat.cs;BroforceDevTools/FPSCounter/StringBuilderNumerics.cs

include Scripts/Makefile.common
