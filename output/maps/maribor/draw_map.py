"""Draw a meter-scaled, editable SVG from layout.json, collision-guides.json and markers.json."""
import json
import math
from pathlib import Path
from html import escape
OUT = Path(__file__).resolve().parent
layout = json.loads((OUT / 'layout.json').read_text())
collisions = json.loads((OUT / 'collision-guides.json').read_text())
markers = json.loads((OUT / 'markers.json').read_text())
S = 5.0
X0, Y0 = -72, -56
W, H = 1180, 1130


def xy(x, y):
    return 60 + (x - X0) * S, 90 + (y - Y0) * S


def rot_rect(c, fill, stroke='none', opacity=1):
    (cx, cy, _), (w, d, _h) = c['center'], c['size']
    r = c.get('rotation_z', 0)
    pts = []
    for lx, ly in ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)):
        x = cx + lx * math.cos(r) - ly * math.sin(r)
        y = cy + lx * math.sin(r) + ly * math.cos(r)
        pts.append('%.1f,%.1f' % xy(x, y))
    return f'<polygon points="{" ".join(pts)}" fill="{fill}" stroke="{stroke}" fill-opacity="{opacity}"/>'


colors = {'A': '#e9a85b', 'B': '#78acbf', 'C': '#ba86ea', 'Shared': '#d0ef75'}
s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
     '<title>Maribor lit-diorama pass — meter-scaled authoring plan</title>',
     '<defs><linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1a1630"/><stop offset="1" stop-color="#4a2430"/></linearGradient></defs>',
     f'<rect width="{W}" height="{H}" fill="url(#sky)"/>',
     '<style>text{font-family:Arial,sans-serif;fill:#eadfcf}.small{font-size:13px}.label{font-size:17px;font-weight:bold}</style>',
     '<text x="50" y="44" font-size="30">MARIBOR / LIT DIORAMA PASS</text>',
     '<text x="50" y="68" class="small">Figma sheets 04 / 05 + Pohorje pixel style · meters · Blender XY / Z up · blue = Drava</text>']
isl = layout['island']
ix0, ix1 = isl['x']
iy0, iy1 = isl['y']
bx0, bx1 = isl['basin']['x']
by0, by1 = isl['basin']['y']
# water, island, basin
s.append(f'<rect x="20" y="80" width="{W - 40}" height="{H - 100}" fill="#14283b"/>')
px0, py0 = xy(ix0, iy0)
px1, py1 = xy(ix1, iy1)
s.append(f'<rect x="{px0}" y="{py0}" width="{px1 - px0}" height="{py1 - py0}" fill="#4a4036" stroke="#8a7a5c"/>')
a, b = xy(bx0, by0)
c_, d_ = xy(bx1, by1)
s.append(f'<rect x="{a}" y="{b}" width="{c_ - a}" height="{d_ - b}" fill="#14283b" stroke="#3a5a78"/>')
# ribbons
for r in layout['ribbons']:
    pts = ' '.join('%.1f,%.1f' % xy(*p) for p in r['points'])
    s.append(f'<polyline points="{pts}" fill="none" stroke="#7b7568" stroke-width="{r['width_m'] * S}" stroke-linejoin="round"/>')
hx = layout['landmarks']['hall']
h0, h1 = xy(*hx[0]), xy(*hx[1])
s.append(f'<rect x="{h0[0]}" y="{h0[1]}" width="{h1[0] - h0[0]}" height="{h1[1] - h0[1]}" fill="#a2704a"/>')
# floors, facade rows and static blockers
for c in collisions:
    if c['dynamic']:
        continue
    if c['name'].startswith('COL_Floor_'):
        k = next((k for k in 'ABC' if c['owner'] == 'MB_Route_' + k), 'Shared')
        s.append(rot_rect(c, '#7b7568', colors[k], .85))
for c in collisions:
    if c['dynamic'] or c['name'].startswith('COL_Floor_'):
        continue
    color = '#c25a3a' if 'Facade' in c['name'] or 'House' in c['name'] else '#4c585e'
    s.append(rot_rect(c, color, 'none', .85))
for c in collisions:
    if c['dynamic']:
        s.append(rot_rect(c, '#df7855', 'none'))
for key, points in layout['paths'].items():
    k = key[0] if key[0] in 'ABC' else next((v for v in 'ABC' if key.endswith(v)), 'Shared')
    coords = ' '.join('%.1f,%.1f' % xy(*p) for p in points)
    dash = 'stroke-dasharray="7 4"' if key in ('C_Reveal', 'B_Alternate') else ''
    s.append(f'<polyline points="{coords}" fill="none" stroke="{colors[k]}" stroke-width="2" {dash}/>')
for m in markers:
    if m['kind'] not in ('StationaryCharge', 'MandatorySideEscape', 'AuthoredGuiltReveal'):
        continue
    x, y = xy(*m['position'][:2])
    color = '#d0ef75' if m['kind'] == 'StationaryCharge' else '#ef624a'
    s.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="4" fill="{color}"/>')
for x, y, label in [(-42, 18, 'A / TABOR'), (0, 18, 'B / LENT'), (60, 18, 'C / PODZEMNI PREHOD')]:
    px, py = xy(x, y)
    s.append(f'<text x="{px:.0f}" y="{py:.0f}" text-anchor="middle" class="label">{label}</text>')
s.append(f'<text x="{xy(0, -50)[0]:.0f}" y="{xy(0, -50)[1]:.0f}" text-anchor="middle" class="label">ARRIVAL FROM POHORJE</text>')
s.append(f'<text x="{xy(0, 153)[0]:.0f}" y="{xy(0, 153)[1]:.0f}" text-anchor="middle" class="label">FERI EXIT</text>')
notes = [('A / TABOR', 'Curving street, car slalom,', '24 × 28 m street court,', 'optional 4.6 m / 1.4 m car ramp.'),
         ('B / LENT', 'Two 3 m lanes round planters,', '22 × 24 m combat quay,', 'basin, causeway and bridge are scenery.'),
         ('C / PODZEMNI PREHOD', 'Guilt at dead end → retreat,', 'side aperture at (55.3, 62),', '18 × 20 m pocket → rear exit.')]
for i, (title, *lines) in enumerate(notes):
    y = 150 + i * 120
    s.append(f'<text x="880" y="{y}" class="label">{title}</text>')
    for j, line in enumerate(lines):
        s.append(f'<text x="880" y="{y + 26 + j * 22}" class="small">{escape(line)}</text>')
for i, line in enumerate(['Green dots: fixed charge props', 'Red dots: reveal / escape markers', 'Orange bars: dynamic gate / rear seal', 'Red-brown: facade collision proposals',
                          'Gate meshes default closed.', 'Only the selected branch activates.', 'No kill quota at arena exits.', 'Geometry proposal; Unity wiring pending.']):
    s.append(f'<text x="880" y="{560 + i * 24}" class="small">{line}</text>')
s.append(f'<path d="M 880 {H - 70} h 100 m -100 -6 v 12 m 100 -12 v 12" stroke="#eadfcf" fill="none"/><text x="880" y="{H - 44}" class="small">20 meters</text>')
s.append('</svg>')
(OUT / 'Maribor_Map.svg').write_text('\n'.join(s) + '\n')
