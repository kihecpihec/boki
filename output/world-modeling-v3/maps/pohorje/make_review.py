"""Generate a portable, script-free render gallery and collision plan from the delivery contract."""
import html
import json
import math
from pathlib import Path

HERE=Path(__file__).resolve().parent
data=json.loads((HERE/'manifest.json').read_text())


def rotate(q,v):
    x,y,z,w=q
    tx=2*(y*v[2]-z*v[1]); ty=2*(z*v[0]-x*v[2]); tz=2*(x*v[1]-y*v[0])
    return (v[0]+w*tx+y*tz-z*ty,v[1]+w*ty+z*tx-x*tz,v[2]+w*tz+x*ty-y*tx)


def hull(points):
    points=sorted(set(points))
    def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lower=[]; upper=[]
    for seq,out in ((points,lower),(list(reversed(points)),upper)):
        for p in seq:
            while len(out)>=2 and cross(out[-2],out[-1],p)<=0:out.pop()
            out.append(p)
    return lower[:-1]+upper[:-1]


COLORS={'Shared':'#a58b57','A':'#60846a','B':'#739cbb','C':'#a47bb5','Backdrop':'#596369'}
parts=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="-110 -28 218 242" role="img" aria-label="Top view of Pohorje with exact collider footprints and gameplay markers"><style>text{font-family:system-ui,sans-serif;fill:#c6d0d1;font-size:5px}.obstacle{fill:#e79667;fill-opacity:.23;stroke:#d88b65;stroke-width:.22}.boundary{fill:none;stroke:#dd9864;stroke-width:.3}.roof{fill:#a47bb5;fill-opacity:.2;stroke:#a47bb5;stroke-width:.25}.walkable{fill:#75b4bd;fill-opacity:.4;stroke:#75b4bd;stroke-width:.2}.seal_front,.seal_rear{fill:#efc56d;stroke:#efc56d;stroke-width:.55}.marker{fill:#d6e5b2;stroke:#233b36;stroke-width:.25}</style><rect x="-110" y="-28" width="218" height="242" rx="3" fill="#19262c"/>']
for pad in data['pads']:
    owner='A' if pad['name']=='ArenaA' else 'B' if pad['name']=='ArenaB' else 'C' if pad['name'] in ('ArenaC','AlcoveC') else 'Shared'
    parts.append(f'<rect x="{pad["cx"]-pad["hx"]}" y="{pad["cz"]-pad["hz"]}" width="{pad["hx"]*2}" height="{pad["hz"]*2}" rx="{pad["radius"]}" fill="{COLORS[owner]}" fill-opacity=".36"/>')
for lane in data['lanes']:
    points=' '.join(f'{p["x"]:.2f},{p["z"]:.2f}' for p in lane['points'])
    parts.append(f'<polyline points="{points}" fill="none" stroke="{COLORS[lane["owner"]]}" stroke-opacity=".7" stroke-width="{lane["width"]}" stroke-linejoin="round" stroke-linecap="round"/>')
for shape in data['colliders']:
    role=shape['role']; center=shape['center']; q=shape['rotation']
    if shape['kind']=='Capsule':
        axis=rotate(q,(0,1,0)); half=shape['height']/2-shape['radius']
        a=(center[0]-axis[0]*half,center[2]-axis[2]*half); b=(center[0]+axis[0]*half,center[2]+axis[2]*half)
        parts.append(f'<line class="obstacle" x1="{a[0]}" y1="{a[1]}" x2="{b[0]}" y2="{b[1]}" style="stroke-width:{shape["radius"]*2}" stroke-linecap="round"/>')
    else:
        points=[]
        for x in (-1,1):
            for y in (-1,1):
                for z in (-1,1):
                    p=rotate(q,(x*shape['size'][0]/2,y*shape['size'][1]/2,z*shape['size'][2]/2))
                    points.append((center[0]+p[0],center[2]+p[2]))
        points=' '.join(f'{x:.2f},{z:.2f}' for x,z in hull(points))
        parts.append(f'<polygon class="{role}" points="{points}"/>')
for marker in data['markers']:
    if marker['kind'] not in ('battery_charge','enemy_spawn','player_start','guaranteed_pickup','stronger_enemy'):continue
    x,_,z=marker['position']
    if marker['kind']=='battery_charge':
        parts.append(f'<path class="marker" d="M {x},{z-1} l 1,2 h -2 Z"/>')
    else:parts.append(f'<circle class="marker" cx="{x}" cy="{z}" r=".7"/>')
for title,x,z in [('BELLEVUE',-9,-4),('FORK',-4,56),('A / LOGGING',-82,137),('B / SERVICE',-18,137),('C / CISTERN',47,134),('OVERLOOK',-12,200)]:
    parts.append(f'<text x="{x}" y="{z}">{title}</text>')
parts.append('</svg>')
svg=''.join(parts)
(HERE/'collision-plan.svg').write_text(svg)

SHOTS=[('04-gondola','Gondola','B / Gondolska servisna pot','Supported plank jumps, carts and a complete gondola line lead into the maintenance yard.'),
       ('03-forest','Forest','A / Gozdna strmina','Spruce slalom, side logs and rock banks open into the timber landing.'),
       ('05-tunnel','Portal','C / Rave tunel','A framed concrete entrance, pixel-painted posters and purple wayfinding.'),
       ('11-tunnel-interior','Grate','C / Guilt reveal bay','The grate opens onto a modeled room; fixtures leave its opening clear.'),
       ('08-cistern','Cistern','C / Reservoir afterparty','A sunken riding floor, columns, two stage ramps, plumbing and an independent canopy.'),
       ('09-overlook','Overlook','Shared / Maribor overlook','A fenced ending frames a solid river valley, bridges and clustered roofs.'),
       ('10-overview','Overview','Whole environment','Shared start, three independent branches and combat spaces, one common ending.')]
css='''*{box-sizing:border-box}body{font:14px var(--font-sans,system-ui);color:var(--foreground,#dce1e3);margin:0}p{line-height:1.5}.stats{font:12px var(--font-mono,monospace);color:var(--muted-foreground,#aab5bc);margin:0 0 14px}input[type=radio]{position:absolute;opacity:0;width:1px;height:1px}label{display:inline-block;padding:7px 10px;margin:0 5px 8px 0;border:1px solid var(--border,#40515c);border-radius:var(--radius,7px);cursor:pointer;font-size:12px}input:focus-visible+label{outline:2px solid var(--ring,#8ab7ba);outline-offset:2px}input:checked+label{background:var(--accent-surface,#274344);color:var(--accent-surface-foreground,#b8e5dd);border-color:var(--accent,#77b4ac)}.areas{height:3px}.gallery figure{display:none;margin:0}.gallery img{display:block;width:100%;height:auto;aspect-ratio:16/9;object-fit:contain;border-radius:var(--radius,7px);background:#18262c}.gallery figcaption{padding:9px 0 4px;line-height:1.5}.gallery strong{font-size:14px}.gallery span{font-size:12px;color:var(--muted-foreground,#aab5bc)}#unity:checked~.gallery .blender,#blender:checked~.gallery .unity{display:none}details{margin-top:15px;border-top:1px solid var(--border,#40515c);padding-top:12px}summary{cursor:pointer;font-size:13px;font-weight:600}details p{font-size:12px;color:var(--muted-foreground,#aab5bc)}.plan-controls{margin-top:12px}.plan-controls label{border:0;padding:4px;margin-right:12px}.plan-controls svg{width:100%;height:auto;display:block;border-radius:var(--radius,7px)}#bounds:not(:checked)~svg .boundary,#roofs:not(:checked)~svg .roof,#markers:not(:checked)~svg .marker{display:none}.evidence{font-size:12px;color:var(--muted-foreground,#aab5bc);margin-bottom:0}'''
for i in range(len(SHOTS)):css+=f'#area{i}:checked~.gallery #view{i}{{display:block}}'
stats=data['expected']
body=f'<p class="stats">{stats["render_triangles"]:,} triangles · {len(stats["materials"])} materials · {len(data["colliders"])} authored shapes · Unity 6000.6.7f1</p>'
body+='<input type="radio" name="engine" id="unity" checked><label for="unity">Unity import</label><input type="radio" name="engine" id="blender"><label for="blender">Blender source</label><div class="areas"></div>'
for i,(name,label,title,description) in enumerate(SHOTS):
    body+=f'<input type="radio" name="area" id="area{i}" {"checked" if i==0 else ""}><label for="area{i}">{label}</label>'
body+='<div class="gallery">'
for i,(name,label,title,description) in enumerate(SHOTS):
    body+=f'<figure id="view{i}"><img class="unity" src="Previews/unity-{name}.png" alt="{html.escape(title)} imported into Unity"><img class="blender" src="Previews/blender-{name}.png" alt="{html.escape(title)} in Blender"><figcaption><strong>{html.escape(title)}</strong><br><span>{html.escape(description)}</span></figcaption></figure>'
body+='</div><details><summary>Inspect the collision plan</summary><p>Exact top-down primitive footprints. Orange: obstacles. Gold: independent front/rear seals. Cyan: ramp/platform collision. Triangles: charges. Dots: standing spawn and pickup positions. Terrain collision follows the single ground surface.</p><div class="plan-controls"><input type="checkbox" id="bounds"><label for="bounds">Bank / outer containment</label><input type="checkbox" id="roofs"><label for="roofs">Roofs</label><input type="checkbox" id="markers" checked><label for="markers">Markers</label>'+svg+'</div></details>'
body+='<p class="evidence">Source and all seven FBXs validated. Imported prefab geometry and materials checked. Floor, traversal, marker and seal tests pass. Production gameplay binding and Windows hardware performance remain to verify.</p>'
page='<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Pohorje v3 review</title><style>'+css+'</style></head><body>'+body+'</body></html>'
(HERE/'Review.html').write_text(page)
print('POHORJE_REVIEW_WRITTEN')
