# Boki player effects

The first effects are two short cyan/magenta boost trails and a brief red damage flash. They are presentation only: they do not change speed, health, invulnerability, input, camera, or route state. They are demonstrated in an isolated preview; connecting them to the gameplay controller and health system is still pending.

## Preview

Open `Game/` in Unity **6000.6.7f1**. Open `Assets/Scenes/BokiEffectsPreview.unity` by itself and press Play. A placeholder scooter follows a circle, emits trails for 0.6 s every 4 s, and flashes every 3 s. Hold **Left Shift** for continuous trails; press **H** for an additional flash. Focus the Game view for keyboard input. These keys and automatic timings belong only to the preview.

If the preview has not been created, run **Boki → Effects → Create Isolated Preview** with saved scenes open (save or close any untitled scenes first). This creates a separate scene and materials without changing existing scenes, prefabs, or build settings. It refuses to overwrite an existing preview. The preview uses unlit geometry and no lights.

## Connect to the player

Coordinate ownership before editing the shared player prefab. Add `BokiPlayerEffects` to the player root, then assign these Inspector fields:

- **Boost Trails:** two child `TrailRenderer` components near the rear of the scooter. Reuse `Assets/Effects/Boki/BoostTrail.mat`. Set Time to 0.25 s, Min Vertex Distance to 0.15, width from 0.12 to 0, and color alpha from 1 to 0. Turn off Emitting, Cast Shadows, and Receive Shadows. Use zero corner/cap vertices.
- **Damage Renderers:** only Boki's body and scooter mesh renderers, excluding trails. Materials need `_BaseColor` (URP Lit/Unlit) or `_Color`. All supported material slots flash.
- **Damage Color / Damage Flash Duration:** default red flash for 0.15 s. This uses scaled time, so pausing freezes the flash.

The owning gameplay scripts call:

```csharp
effects.SetBoostActive(isBoosting); // Actual controller state, including cancellation.
effects.PlayDamageFlash();         // Only after health accepts damage.
effects.ClearTrails();             // Immediately after teleporting or respawning.
```

Call `SetBoostActive(false)` when a boost ends, on death, or when disabling just the controller. Disabling the effects component or its GameObject also clears trails and restores colors. Repeated damage restarts the flash without replacing the original color. Reused objects start with no trails and no flash; the controller must supply the current boost state again.

The flash uses per-material `MaterialPropertyBlock` overrides, without cloning or changing shared materials. It restores the blocks captured before the flash, including falling back to renderer-wide overrides. Coordinate with any other system that writes those same blocks during a flash; its changes could be replaced on restoration.

## Verification

- In the preview, confirm short tapering trails appear during boost and fade afterward. Check both are visible and no material is pink.
- Press H repeatedly; both body parts should flash and return to cyan. Shared materials and other objects must retain their colors.
- Disable/re-enable `BokiPlayerEffects` during a flash and boost; colors must restore and trails must clear. Resume with the controller's next boost update.
- Teleport during boost and call `ClearTrails()` afterward; no line should connect the old and new positions.
- Pause with `Time.timeScale = 0`; the flash and preview movement should freeze together.
- Check the preview and eventual gameplay player in Play Mode for Console errors. Verify the Windows player on the target Ryzen 5600G separately; Linux batch validation does not establish visual quality or Windows FPS.

Only two short unlit trails are used: no particles, extra lights, post-processing, or per-frame material allocations. Gameplay effect budgets remain in [Gameplay.md §8](Gameplay.md#8-performance-target-and-unity-settings).

Initial validation on Linux with Unity 6000.6.7f1: asset import and compilation passed, and a temporary batch Play Mode smoke check passed boost start/stop, trail clearing, repeated damage, disable/re-enable cleanup, timed color restoration, renderer-wide override preservation, shared-material preservation, and shader import error checks. All new assets have Editor-generated `.meta` files. Unity also logged an internal `UnityEditor.Search.SearchDatabase` indexing exception during Play Mode startup; it did not stop the smoke check. Headless checks do not verify rendered appearance, keyboard interaction, or target Windows performance; those manual checks remain pending.
