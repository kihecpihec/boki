# HUD revision: Guilt proximity and centered XP

Created with built-in imagegen on feature/hud-mockup. Visual concept only, no runtime or scene changes.

User-directed presentation: replace GUILT NEARBY text with a subtle red screen-edge tint when Guilt is nearby; center XP horizontally at the bottom. This proximity feedback does not change the existing Panic Mode upgrade eligibility or gameplay rules. In implementation, keep the tint restrained and switchable, consistent with Documentation/Gameplay.md. Reviewed the output for removed text, centered XP, subdued colors and readable HP/battery.

## Final prompt

Edit this Boki barebones PS1 horror HUD mockup. Preserve the exact scene composition, character, dark desaturated colors, simple bitmap font, HP and BAT bars bottom left, POHORJE top left, 07:58 top right, and boost text bottom right.
Make only these changes:
1. Completely remove 'GUILT NEARBY' from upper center, restoring unobstructed sky there.
2. Add a very faint muted dark-red translucent vignette only at all four outer screen edges to signal nearby Guilt/panic. It must fade quickly to transparent inward, leaving the central 80% clear and unchanged. Subtle horror warning, not bright neon, not a solid frame, no text or icons. Preserve HUD legibility.
3. Remove XP label and bar from bottom left. Relocate them to the horizontal middle of the screen near the bottom edge (bottom center), centered around x=50%, at the same vertical level as the previous XP bar. Use small off-white pixel text 'XP 9/12' just above a thin flat gray progress bar with 40% filled and dark unfilled background. Approximately the same width as old XP bar, no decorations. Do not place XP in the middle of gameplay vertically.
No new UI elements or style changes. Keep barebones flat rectangles and muted PS1 horror presentation.
