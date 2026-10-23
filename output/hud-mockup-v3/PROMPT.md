# Barebones HUD concept

Generated with built-in imagegen, simplifying v2 per user direction. Visual concept only. No Unity assets changed.

Implementation intent: bitmap text and three flat fill/background pairs, without UI lighting, blur, glow, animated materials or decorative sprites. Static concept cannot establish runtime performance; verify a Windows player build on Ryzen 5600G integrated graphics during implementation. Guilt message is conditional during warning/chase, not permanent. XP 9/12 is progress between cumulative thresholds 7 and 12; fill should be 40% in implementation (generated fill is approximate).

## Final prompt

Use case: ui-mockup. Revise the attached Boki HUD concept into a BAREBONES authentic PS1-era indie horror gameplay HUD. The user explicitly rejects modern UI, ornate frames, bright colors and excessive detail. Produce one full-screen 16:9 gameplay mockup, not a presentation sheet.
Reference is the previous concept to simplify. Keep Boki riding a scooter in third person on a dark Pohorje forest trail, purple jacket with a very faded desaturated lime stripe. Completely tone down the entire image: near-black pines, muddy gray-brown ground, dense dull blue-gray fog, charcoal sky. No pink or violet sky, no bright green glow, no saturated purple, no bloom. Low-poly angular PS1 geometry, very sparse trees and simple terrain, coarse 64px textures, short fog draw distance, flat lighting. Actual low-budget 1997 game screenshot feel. One or two distant plain humanoid shadows, no horned or siren-headed creatures. Small dull yellow battery pickup by trail. Readable route ahead. Rough pixel edges, not modern cinematic graphics.
HUD MUST BE EXTREMELY SIMPLE, like a solo PS1 developer built it from flat rectangles and a bitmap font:
- Off-white medium-small pixel text only, no fancy font, no antialiasing.
- Top left: 'POHORJE' only. No subtitle, route plate, decorative line or panel.
- Top right: '07:58' in the SAME small plain bitmap font as location. No watch housing, no seven-segment clock, no FERI label.
- Bottom left a compact stack of THREE thin horizontal bars about 150px wide in a 960px-wide layout, each 5px high, all with solid near-black empty background and absolutely flat fills. Text line above first: 'HP 80/100'. Fill desaturated dark brick red at 80%. Text above second: 'BAT 64/100'. Fill dull gray olive at 64%. Text above third: 'XP 9/12'. Fill medium neutral gray at 40%. No icons, no borders beyond optional one-pixel dark outline, no segmented ticks, no texture, no bevel, no metallic material, no gradients, no glow, no enclosing panel.
- Bottom right small plain text 'SHIFT: BOOST READY'. No boost icon, no keycap, no progress bar, no purple.
- Small subtle off-white 'GUILT NEARBY' near upper center, same plain pixel font. No triangle, no red, no banner.
- Remove upgrades count, inventory, ornaments, minimap, crosshair, kill counter, all other HUD.
The scene should occupy almost the whole view with tiny restrained functional HUD anchored to corners with safe margins. No UI elements anywhere across bottom center. No thick borders. No excessive grain, scanline or chromatic effects. This is horror, understated and bleak. Simplicity and readability are the main goal. Feasible as a few text objects and six flat UI rectangles on Ryzen 5600G integrated graphics. Do not print any explanatory text or hardware specifications in image.
