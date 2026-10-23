"""Draw the current meter-scaled action layout from its source manifests."""
import json,math
from pathlib import Path
from xml.sax.saxutils import escape
OUT=Path(__file__).resolve().parent
layout=json.loads((OUT/'layout.json').read_text())
markers=json.loads((OUT/'markers.json').read_text())
instances=json.loads((OUT/'forest-instances.json').read_text())
colors={'A':'#cbb893','B':'#8cc7c1','C':'#bd8bdf'}
parts=['<svg xmlns="http://www.w3.org/2000/svg" width="1400" height="1300" viewBox="0 0 1400 1300">',
'<title>Pohorje: compact action layout</title><rect width="1400" height="1300" fill="#111b20"/>',
'<g fill="#e3e7dc" font-family="sans-serif">',
'<text x="70" y="65" font-size="32">POHORJE / STRAIGHT INTO THE ACTION</text>',
'<text x="70" y="105" font-size="19">Early encounter candidates ~4 s from start · arena entrances ~8–11 s · baseline 6 m/s, no stops</text>',
'<g transform="translate(700 280) scale(5)">',
'<rect x="-115" y="-27" width="230" height="162" fill="#263a32"/>']
for x in range(-100,101,20):parts.append(f'<path d="M{x},-27 V135" stroke="#4d6151" stroke-width=".12"/>')
for y in range(-20,136,20):parts.append(f'<path d="M-115,{y} H115" stroke="#4d6151" stroke-width=".12"/>')
for r in instances:
    x,y,z=r['xyz'];parts.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r=".9" fill="#13251e"/>')
for key,pts in layout['paths'].items():
    color=colors.get(key[-1],'#c8cbb8');points=' '.join(f'{x},{y}' for x,y in pts)
    parts.append(f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="{layout["widths"][key]}" stroke-linejoin="round"/>')
for k,(x,y,w,d) in layout['pockets'].items():
    parts.append(f'<rect x="{x-w/2}" y="{y}" width="{w}" height="{d}" fill="#31463d" stroke="{colors[k]}" stroke-width=".5"/>')
    parts.append(f'<text x="{x}" y="{y+7}" font-size="3.2" text-anchor="middle" fill="{colors[k]}">{k} / {w} × {d} m</text>')
for feature in layout['features']:
    x,y=feature['xy'];k=feature['route']
    parts.append(f'<circle cx="{x}" cy="{y}" r="1.2" fill="{colors[k]}"/>')
    parts.append(f'<text x="{x}" y="{y+4}" text-anchor="middle" font-size="2.1">{escape(feature["name"])}</text>')
for m in markers:
    x,y,z=m['xyz']
    if m['kind'].startswith('stationary'):parts.append(f'<circle cx="{x}" cy="{y}" r="1.1" fill="#6bf3bf"/>')
    if m['kind'].startswith('early regular'):parts.append(f'<circle cx="{x}" cy="{y}" r="1.2" fill="#ed946b"/>')
    if m['kind']=='gate assembly':
        a=m['angle'];r=m['clear_width']/2;dx=r*math.cos(a);dy=r*math.sin(a)
        parts.append(f'<path d="M{x-dx},{y-dy} L{x+dx},{y+dy}" stroke="#edce83" stroke-width=".7"/>')
parts.extend(['<text x="0" y="-23" text-anchor="middle" font-size="3.8">BELLEVUE / START</text>',
'<text x="0" y="128" text-anchor="middle" font-size="3.8">CITY OVERLOOK / EXIT</text>',
'<path d="M-108,124 H-88" stroke="#e3e7dc" stroke-width=".7"/><text x="-98" y="121" text-anchor="middle" font-size="2.8">20 m</text></g>'])
for i,k in enumerate('ABC'):
    r=layout['stats']['route_stats'][k]
    parts.append(f'<text x="70" y="{1045+i*40}" font-size="21" fill="{colors[k]}">{k}: {r["centerline_m"]} m full route · {r["approach_m"]} m to arena · {r["approach_seconds_at_6mps"]} s at baseline</text>')
parts.extend(['<text x="70" y="1195" font-size="18">Gold: front/rear seals · orange: early encounter candidates · green: charges · grid: 20 m</text>',
'<text x="70" y="1230" font-size="18">Only the selected route activates. C remains the shorter secret route. Arena exits stay open.</text>',
'<text x="70" y="1265" font-size="17">Blender authoring layout; runtime encounters, controller traversal and Windows performance require Unity checks.</text></g></svg>'])
(OUT/'Pohorje_Large_Map.svg').write_text('\n'.join(parts)+'\n')
