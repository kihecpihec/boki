# Windows build

Place the complete Windows x86-64 player distribution here, including the `.exe`, matching `_Data/` directory, `UnityPlayer.dll`, `MonoBleedingEdge/`, and any other files generated for the player. Keep each architecture/version in its own subdirectory. Do not copy only the executable.

No Windows player binary is present in this checkout. Build with Unity **6000.6.7f1** and Windows Build Support (Mono), following the [V2 build instructions](../Documentation/plans/v2-windows-arm64-build.md). Use the x86-64 variant for the Ryzen 5600G target. Select a directory under this folder when building interactively.

Distribute the complete player folder, excluding Unity's `BackUpThisFolder_ButDontShipItWithYourGame` directory and build logs. Validate startup, gameplay, and performance on Windows before release.
