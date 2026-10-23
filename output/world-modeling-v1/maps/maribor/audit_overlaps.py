"""Report solid collision guides that sink into each other. Pure Python; reads collision-guides.json.

Run: python3 -I audit_overlaps.py     (writes overlap-audit.json, exits 1 when buildings overlap)

Buildings (FacadeBay, ParkHouse) may touch but never penetrate by more than TOLERANCE metres. Other
solids are reported for review; rails meeting at corners, props on the terrace slab and same-material concrete joints are expected.
"""
import itertools
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOLERANCE = .2
BUILDINGS = ('FacadeBay', 'ParkHouse')
STANDS_ON = ('TerraceWall',)   # a slab the guide set models as a full-height wall; lamps and trees stand on its top
EXPECTED = ('Boundary', 'QuayEdge', 'GatePost', 'PlazaNorthWallW', 'PlazaNorthWallE', 'PlazaNorthMid', 'Planter',
            # Poured-concrete walls of the underpass meet in T and corner joints of one material.
            'TunnelWest', 'TunnelEast', 'EscapeConnectorWall', 'EscapeWall', 'GuiltEndWall', 'PocketWall', 'PocketReturn')


def base(name):
    return name[4:].split('.')[0]


def corners(g):
    x, y, _ = g['center']
    w, d, _ = g['size']
    r = g.get('rotation_z', 0)
    c, s = math.cos(r), math.sin(r)
    return [(x + px * c - py * s, y + px * s + py * c) for px, py in ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2))]


def depth(a, b):
    ca, cb = corners(a), corners(b)
    best = 1e9
    for g in (a, b):
        r = g.get('rotation_z', 0)
        for ax in ((math.cos(r), math.sin(r)), (-math.sin(r), math.cos(r))):
            pa = [p[0] * ax[0] + p[1] * ax[1] for p in ca]
            pb = [p[0] * ax[0] + p[1] * ax[1] for p in cb]
            o = min(max(pa), max(pb)) - max(min(pa), min(pb))
            if o <= 0:
                return 0.
            best = min(best, o)
    return best


def z_range(g):
    return g['center'][2] - g['size'][2] / 2, g['center'][2] + g['size'][2] / 2


def main():
    guides = [g for g in json.loads((HERE / 'collision-guides.json').read_text())
              if not g['dynamic'] and not g['name'].startswith('COL_Floor_')]
    buildings, others = [], []
    for a, b in itertools.combinations(guides, 2):
        za, zb = z_range(a), z_range(b)
        if min(za[1], zb[1]) - max(za[0], zb[0]) <= .05:
            continue
        d = depth(a, b)
        if d <= TOLERANCE:
            continue
        pair = (a['name'], b['name'], round(d, 2), [round(v, 1) for v in a['center'][:2]])
        if base(a['name']) in BUILDINGS and base(b['name']) in BUILDINGS:
            buildings.append(pair)
        elif not (base(a['name']) in STANDS_ON or base(b['name']) in STANDS_ON or (base(a['name']) in EXPECTED and base(b['name']) in EXPECTED)):
            others.append(pair)
    report = dict(tolerance_m=TOLERANCE, building_overlaps=buildings, other_solid_overlaps=others,
                  status='passed' if not buildings else 'failed')
    (HERE / 'overlap-audit.json').write_text(json.dumps(report, indent=2) + '\n')
    print('building overlaps:', len(buildings), '| other solid overlaps:', len(others))
    for pair in buildings + others:
        print(' ', pair)
    return 1 if buildings else 0


if __name__ == '__main__':
    sys.exit(main())
