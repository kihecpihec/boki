# Reference-driven HUD mockup

Generated using the built-in imagegen tool. References reviewed from output/pictures; selected inputs listed in the final prompt below. Character reference: output/character-modeling-v2/references/02-player-riding-and-scooter.png.

Visual concept only, with illustrative HUD values. No Unity assets or scenes changed. Reviewed layout and text visually; XP progress is approximately 40% between cumulative thresholds 7 and 12. Generated enemy silhouettes are atmospheric placeholders, not approved enemy designs. Canonical gameplay remains in docs/Gameplay.md.

## Final prompt

Use case: ui-mockup. Generate a single full-screen 16:9 BOKI: LATE TO FERI gameplay HUD mockup. Reference 1 (pink sky castle screenshot) supplies HUD language ONLY: small old-school inset rough dark beveled bars, flat colored fills, almost no surrounding boxes. Reference 2 (Murder House) supplies coarse low-resolution texture, restrained VHS unease. Reference 3 supplies silhouetted angular forest and dirty violet sky, not its monster identity, title or logo. Reference 4 supplies Boki identity and scooter. These are style references, create original Boki gameplay.
Make this look like an actual 1998 low-resolution survival horror game rendered internally at 640x360 then enlarged with nearest-neighbor: visibly chunky pixels, unfiltered grimy tiny textures, faceted low-poly geometry, dithered fog, flat lighting, limited colors. Avoid modern high fidelity 3D, smooth cinematic volumetrics, photorealistic materials, hair strands, dense foliage. A third-person rear view of Boki in purple jacket with lime shoulder stripe riding black-purple electric scooter down a Pohorje spruce forest trail. Cold blue-black foreground, dirty purple dusk sky and fog, one warm orange distant lamp, clear path and two small angular enemy silhouettes. A tiny green battery pickup beside the trail. No giant foreground monster. Background lightly grainy; crisp readable pixel font HUD unaffected by grain.
HUD sparse and period-authentic with 5% safe margins, open center:
TOP LEFT: small ivory pixel text 'POHORJE', second line smaller 'A / GOZDNA STRMINA'. No enclosing card.
TOP RIGHT: distinctive simple dark rectangular wristwatch-style readout '07:58', ivory seven-segment digits; tiny 'FERI' underneath. Small clock, no countdown.
BOTTOM LEFT: two 210px-wide shallow inset dark charcoal beveled bars stacked, first muted blood red 80% filled, second sickly lime 64% filled. Above first bar small pixel cross, 'HP' and right aligned '80/100'. Above second small pixel battery, 'BATTERY' and right aligned '64/100'. Pixel-thick dark gray worn borders, no outer panel. Each label highly readable.
BOTTOM CENTER: extremely thin muted violet XP strip 40% filled with small 'XP 9/12' and 'UPGRADES 2/6' above; leave scooter clearly visible.
BOTTOM RIGHT: small square pixel lightning glyph, 'BOOST', small '[L SHIFT] READY', short violet full ready bar. Same dark worn bevel language. No outer card.
TOP CENTER: a small red triangle and red pixel 'GUILT NEARBY' with lots of negative space, no banner, no monster health bar.
No minimap, no crosshair, no ammo, no speedometer, no fantasy ornaments, no giant UI icons, no rounded modern cards, no logos or watermarks, no illustrative presentation board. No title outside game view. Less polished electronic dashboard, more tactile battered PS1 survival horror. Health red, battery lime, boost/XP violet; off-white text. Strong readable game HUD is the deliverable.
