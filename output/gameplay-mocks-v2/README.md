# BOKI / gameplay mock update

Open [index.html](index.html) for the complete gallery or [ui-states.html](ui-states.html) for HUD and reward presentation.

- [sheets/](sheets/): five editable SVG sheets: Pohorje, Maribor, FERI, shared boss recovery, and UI states.
- [renders/](renders/): matching PNG images for review and image-based handoff.
- [combat-space-plans.json](combat-space-plans.json): proposed pocket dimensions and charge/spawn/Guilt coordinates, with canonical-document hashes.
- [HANDOFF.md](HANDOFF.md): scene assignments, isolation/timer rules, recorded proposals, and pending Editor checks.
- [build.py](build.py): reproducible HTML/SVG/JSON generator; optional PNG rendering through CairoSVG.

Use this folder beside `world-modeling-v1/` and `character-modeling-v2/` under `output/`. If using ZIPs, extract all three packs beside one another. The old galleries link to this update and the relevant world sections show its PNG plans beside the original art.

The original reference PNGs remain shape and atmosphere references. These code-native overlays add gameplay planning without regenerating or editing the raster artwork. Canonical rules remain in `docs/`; new dimensions, marker coordinates, and Lent's extra charge are proposals for playtesting. Figma and Unity assets are unchanged.

Rebuild with `python3 output/gameplay-mocks-v2/build.py`. Rebuild including PNGs with `uv run --with cairosvg python output/gameplay-mocks-v2/build.py --render`.
