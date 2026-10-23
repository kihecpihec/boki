# Explore Pohorje in Blender

Open `Source/Pohorje_Large.blend` and choose the `Pohorje_Large` scene. The saved 3D viewport starts at eye level at Bellevue and faces the approach to the fork. Solid shading is enabled for responsive navigation.

1. Hover over the main 3D viewport.
2. Press **F3**, type **Walk Navigation**, then press **Enter**. This works without a particular keyboard layout. Alternatively choose **View → Navigation → Walk Navigation**, or press **Shift + grave accent** (the backtick key on a US keyboard).
3. Move the mouse to look; use **W/A/S/D** to move.
4. Scroll to change speed. Hold **Shift** for a temporary speed boost.
5. With gravity off, **Q/E** moves down/up. **Tab** toggles gravity; **V** jumps when gravity is on.
6. **Left-click** keeps the new viewpoint and exits navigation. **Esc** cancels and returns to the starting viewpoint.

Keep gravity off initially so you can freely inspect everything. Turn it on with Tab when above a path if you want to stay near the floor. This is Blender viewport navigation, not the game's movement, collision, gate, or route-selection system. You can inspect all three branches in one session; the art does not run gameplay logic. Use Q/E to bypass parked gate art when inspecting.

Press **Z**, then **M** for Material Preview with textures. Press **Z**, then **S** to return to fast Solid shading. The `Previews` folder shows the presentation lighting and fog; those effects are more expensive while navigating.

If you get lost, press **Home** to frame the map, then **Numpad 7** for a top-down view. Select a path object in the Outliner and press **Numpad .** to frame it. If you lack a numpad, use **View → Frame Selected**. Leave camera view before walking so navigation does not reposition a presentation camera; the delivered starting viewport is already outside camera view.

Reference: [Blender's Fly/Walk Navigation manual](https://docs.blender.org/manual/en/latest/editors/3dview/navigate/walk_fly.html).
