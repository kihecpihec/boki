"""Draw a meter-scaled planning map from the built geometry's layout manifest."""
import json
import math
from pathlib import Path
from xml.sax.saxutils import escape

OUT=Path(__file__).resolve().parent
data=json.loads((OUT/'layout.json').read_text())
markers=json.loads((OUT/'markers.json').read_text())
instances=json.loads((OUT/'forest-instances.json').read_text())
parts=['<svg xmlns="http://www.w3.org/2000/svg" width="1400" height="2100" viewBox="0 0 1400 2100">',
       '<title>Pohorje large level: meter-scaled authoring map</title>',
       '<rect width="1400" height="2100" fill="#111a1c"/>',
       '<g font-family="sans-serif" fill="#d2d4c5">',
       '<text x="70" y="65" font-size="32">POHORJE / WRONG WAY HOME</text>',
       '<text x="70" y="105" font-size="19">Large environment build · meters · Blender Z up · player movement and Windows performance pending</text>',
       '<text x="70" y="140" font-size="18">Shared start → one branch → open combat space → sealed merge → city overlook</text>',
       '<g transform="translate(700 245) scale(2.8)">',
       '<rect x="-192" y="-32" width="384" height="576" fill="#20302b" stroke="#4e6159" stroke-width=".5"/>']
for x in range(-150,200,50):parts.append(f'<path d="M{x},-32 V544" stroke="#3a4941" stroke-width=".2"/>')
for y in range(0,550,50):parts.append(f'<path d="M-192,{y} H192" stroke="#3a4941" stroke-width=".2"/>')
for r in instances:
    if 'Spruce' not in r['asset'] and 'Dead' not in r['asset']:continue
    x,y,z=r['xyz'];parts.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="1.2" fill="#101f1d"/>')
colors={'A':'#c7b69a','B':'#91afa8','C':'#ac84b3'}
for key,points in data['paths'].items():
    route=key[-1] if key[-1] in 'ABC' else None;color=colors.get(route,'#c0c5a9')
    points=' '.join(f'{x:.3f},{y:.3f}' for x,y in points)
    parts.append(f'<polyline points="{points}" fill="none" stroke="#111a1c" stroke-width="{data["widths"][key]+4}" stroke-linejoin="round"/>')
    parts.append(f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="{data["widths"][key]}" stroke-linejoin="round"/>')
for route,(x,y,w,d) in data['pockets'].items():
    color=colors[route]
    parts.append(f'<rect x="{x-w/2}" y="{y}" width="{w}" height="{d}" fill="#283732" stroke="{color}" stroke-width="1"/>')
    parts.append(f'<text x="{x}" y="{y+d/2}" font-size="7" text-anchor="middle" fill="{color}">{route} / {w} × {d} m</text>')
    parts.append(f'<text x="{x}" y="{y+d/2+8}" font-size="3.5" text-anchor="middle">OPEN EXIT · NO KILL QUOTA</text>')
for m in markers:
    x,y,z=m['xyz']
    if m['kind'].startswith('stationary'):
        parts.append(f'<circle cx="{x}" cy="{y}" r="1.6" fill="#74c5ae"/>')
    if m['kind']=='gate assembly':
        w=m['clear_width'];angle=m['angle'];dx=math.cos(angle)*w/2;dy=math.sin(angle)*w/2
        parts.append(f'<path d="M{x-dx},{y-dy} L{x+dx},{y+dy}" stroke="#e3c586" stroke-width="1.2"/>')
        if 'Front' in m['name']:parts.append(f'<text x="{x}" y="{y-5}" text-anchor="middle" font-size="5">{m["route"]}</text>')
parts.extend(['<text x="0" y="-17" font-size="7" text-anchor="middle">BELLEVUE / SAFE START</text>',
              '<text x="0" y="71" font-size="5" text-anchor="middle">THREE-WAY DECISION ROOM</text>',
              '<text x="0" y="539" font-size="7" text-anchor="middle">CITY OVERLOOK / ExitForest</text>',
              '<path d="M-177,515 H-127 M-177,513 V517 M-127,513 V517" stroke="#d2d4c5" stroke-width=".7"/>',
              '<text x="-152" y="511" text-anchor="middle" font-size="4">50 m</text>', '</g>'])
for i,k in enumerate('ABC'):
    r=data['stats']['route_stats'][k]
    parts.append(f'<text x="70" y="{1885+i*36}" font-size="21" fill="{colors[k]}">F_{k}: {r["centerline_m"]:.1f} m route · {r["lane_width_m"]} m main lane · {r["combat_dimensions_m"][0]} × {r["combat_dimensions_m"][1]} m combat space</text>')
parts.extend(['<text x="70" y="2025" font-size="17">C remains the shorter secret route. Three fixed charges per pocket. Only the chosen branch activates.</text>',
              '<text x="70" y="2060" font-size="17">Gold lines: front/rear seals · green dots: charges · grid: 50 m · all dimensions require controller playtests.</text>',
              '</g></svg>'])
(OUT/'Pohorje_Large_Map.svg').write_text('\n'.join(parts)+'\n')
print('Wrote',OUT/'Pohorje_Large_Map.svg')
