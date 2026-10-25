"""Build editable, code-native gameplay diagrams. Run from any directory."""
from pathlib import Path
from html import escape
import argparse
import hashlib
import json

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
INK, PAPER, MUTED = '#090e14', '#d9e1cf', '#a3b4b6'
ACID, PURPLE, RED, BLUE = '#d0ef75', '#ba86ea', '#ef624a', '#628d9f'


def text(x, y, content, size=20, color=PAPER, bold=False):
    # Cairo's portable monospace fallback lacks arrow glyphs; use readable ASCII.
    content = str(content).replace('→', '->').replace('↑', '^')
    return f'<text x="{x}" y="{y}" fill="{color}" font-size="{size}" font-weight="{700 if bold else 400}" font-family="monospace">{escape(content)}</text>'


def lines(x, y, content, size=20, color=MUTED, step=30):
    return ''.join(text(x, y + i * step, line, size, color) for i, line in enumerate(content))


def rect(x, y, w, h, fill='#121c24', stroke=BLUE, dash=False):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="{stroke}" stroke-width="2"' + (' stroke-dasharray="8 6"' if dash else '') + '/>'


def line(x1, y1, x2, y2, color=BLUE, width=3):
    return f'<path d="M{x1} {y1} L{x2} {y2}" stroke="{color}" stroke-width="{width}" fill="none"/>'


def charge(x, y, label):
    return rect(x - 14, y - 18, 28, 36, ACID, ACID) + text(x - 7, y + 7, '+', 22, INK, True) + text(x + 20, y + 7, label, 16, ACID)


def spawn(x, y, label):
    return f'<circle cx="{x}" cy="{y}" r="9" fill="{RED}"/>' + text(x + 14, y + 6, label, 14, RED)


def svg(name, title, parts, height=1800):
    body = f'<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="{height}" viewBox="0 0 1600 {height}" role="img" aria-labelledby="title"><title id="title">{escape(title)}</title><rect width="1600" height="{height}" fill="{INK}"/>'
    body += ''.join(parts) + '</svg>'
    (HERE / 'sheets' / (name + '.svg')).write_text(body)


LEVELS = [
    ('forest', 'POHORJE / CHOOSE, FIGHT, LEAVE', 4, 8, 4, [
        ('F_A', 'Gozdna strmina', 20, 18, ['Spruce slalom → clearing', 'Melee emphasis / more XP'], 'Scripted Guilt behind Boki'),
        ('F_B', 'Gondola service path', 20, 18, ['Jumps → service clearing', 'Fewer enemies / late Watcher'], 'Static gondola; no new system'),
        ('F_C', 'Rave tunel', 20, 18, ['Tunnel → widened rave room', 'Two stronger regular enemies'], 'Grate event; B1 guaranteed')]),
    ('city', 'MARIBOR / STAY FOR ANOTHER UPGRADE?', 6, 12, 3, [
        ('C_A', 'Tabor', 24, 20, ['Car slalom → street court', 'Two authored melee groups'], 'Car-ramp escape; Guilt roof'),
        ('C_B', 'Lent', 24, 18, ['Narrow lanes → riverside yard', 'Ranged Watchers / no water travel'], 'B4: extra recovery proposal'),
        ('C_C', 'Podzemni prehod', 20, 18, ['Gallery → service room', 'Fewer regular enemies'], 'Guilt turn-back → side door')]),
    ('feri', 'FERI / ONE LAST COMBAT SPACE', 4, 8, 4, [
        ('E_A', 'Glavni vhod', 20, 18, ['Corridor → seminar room', 'Two authored melee groups'], 'Standard professor fight'),
        ('E_B', 'Dvorišče', 20, 18, ['Ambush → courtyard pocket', 'Ramp → shared landing'], 'Boss: two melee at half HP'),
        ('E_C', 'Servisni vhod', 20, 18, ['Utility hall → service room', 'Two floor traps on approach'], 'Optional POWER OFF → 85% HP')])]

snapshot = {
    'status': 'visual handoff only; no gameplay implementation',
    'canonicalSources': {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in [ROOT / 'docs/Gameplay.md', ROOT / 'docs/Story.md']},
    'spatialStatus': 'All new pocket extents, marker coordinates and Lent B4 are authoring proposals requiring playtests.',
    'coordinates': 'Pocket-local meters: X right, Z toward exit; Y=0. Inset plans are 14 SVG pixels per meter.',
    'rulesReference': '../../docs/Gameplay.md#5-loop-within-each-route',
    'levels': [],
    'boss': {'roomM': [24, 20], 'clearFloorM': [14, 12], 'chargePositionsXZ': [[4, 8], [20, 8]], 'startingHp': 4000, 'powerOffStartingHp': 3400},
}


def build_level(key, title, desired, cap, cadence, routes):
    parts = [text(60, 65, 'BOKI / GAMEPLAY MOCKS V2 / DOC-SYNC OVERLAY', 20, ACID), text(60, 130, title, 42, PAPER, True)]
    parts += [lines(60, 180, ['Three alternatives shown for authoring; only the chosen branch activates.', 'Topology above / meter-scaled combat insets below / extents are proposals.'])]
    parts += [rect(60, 270, 1480, 60), text(90, 309, 'SHARED START → filtered fork / run-start unlock snapshot', 24, ACID)]
    level_data = {'id': key, 'desiredRegularEnemies': desired, 'maxRegularEnemies': cap, 'spawnCadenceActiveSeconds': cadence, 'routes': []}
    colors = [ACID, BLUE, PURPLE]
    for i, (route, name, w, h, beats, event) in enumerate(routes):
        x = 60 + i * 500
        color = colors[i]
        parts += [line(x + 220, 330, x + 220, 365, color), rect(x, 365, 480, 110, stroke=color), text(x + 18, 401, route + ' / ' + name, 22, color, True), text(x + 18, 441, 'ENTRY GATE / other entries seal', 18)]
        parts += [line(x + 220, 475, x + 220, 510, color), rect(x, 510, 480, 110, stroke=color), lines(x + 18, 550, beats, 18, PAPER)]
        parts += [line(x + 220, 620, x + 220, 655, color), rect(x, 655, 480, 110, '#23393c', color), text(x + 18, 693, 'COMBAT SPACE / stay or leave', 21, color, True), text(x + 18, 731, 'Exit stays open; no kill quota', 18)]
        if route == 'C_C':
            parts += [text(x + 18, 807, 'After pocket: reveal → retreat', 17, RED), text(x + 18, 832, '→ SIDE DOOR (required) → rear seal', 17, RED)]
        else:
            parts += [text(x + 18, 807, event, 17, MUTED)]
        parts += [line(x + 220, 765, x + 220, 780, color), rect(x, 855, 480, 60, stroke=RED), text(x + 18, 893, 'REAR SEAL / selected branch opens', 18, RED)]
        px, py, scale = x + 30, 1130, 14
        parts += [text(x, 1075, f'{route} POCKET / {w} × {h} m proposal', 22, color, True), rect(px, py, w * scale, h * scale, '#17282d', color, True)]
        # Dashed edge is the pressure/spawn volume, never a physical combat lock.
        for z in range(4, h, 4):
            parts += [line(px, py + z * scale, px + w * scale, py + z * scale, '#263b49', 1)]
        positions = [[4, 4], [w - 4, 4], [w / 2, h - 4]]
        if route == 'C_B':
            positions.append([w / 2, h / 2])
        for n, (cx, cz) in enumerate(positions, 1):
            parts += [charge(px + cx * scale, py + (h - cz) * scale, f'B{n}')]
        spawn_positions = [[1, 1], [w - 1, 1], [1, h / 2], [w - 1, h / 2], [1, h - 1], [w - 1, h - 1]]
        for n, (sx, sz) in enumerate(spawn_positions, 1):
            parts += [spawn(px + sx * scale, py + (h - sz) * scale, f'S{n}')]
        guilt_positions = [[w/2, 2], [w/2+3, h/2], [w/2, h-2]]
        for n, (gx, gz) in enumerate(guilt_positions, 1):
            ax, ay = px + gx*scale, py + (h-gz)*scale
            parts += [f'<path d="M{ax} {ay-9} l9 9 -9 9 -9 -9 Z" fill="{PURPLE}"/>', text(ax+14, ay+6, f'G{n}', 14, PURPLE)]
        parts += [text(px + 85, py - 16, 'FREE EXIT ↑', 18, color), text(px + 90, py + h * scale + 32, '↑ ENTRY', 18, color)]
        parts += [lines(x, 1520, ['B = fixed contact charge (+25)', 'S = candidate regular-enemy spawn', 'G = Guilt anchor: offscreen, behind', 'Boki; only chosen pocket active.'], 17)]
        level_data['routes'].append({'id': route, 'name': name, 'pocketM': [w, h], 'chargesXZ': positions, 'spawnCandidatesXZ': spawn_positions, 'guiltCandidatesXZ': guilt_positions, 'authoredEvent': event, 'extraChargeStatus': 'Lent B4 is a proposed recorded route variation' if route == 'C_B' else None})
    parts += [rect(60, 940, 1480, 65), text(85, 982, 'SHARED END / no access to unchosen branches; retain opaque rear blockers', 23, RED)]
    for i in range(3):
        parts += [line(280+i*500, 915, 280+i*500, 940, colors[i])]
    parts += [lines(60, 1690, [f'Replenish: desired {desired}, total cap {cap}, one every {cadence} active seconds. Offscreen + ≥8 m; invalid point → wait.', 'Unchosen enemies/charges inactive; hide interiors. Scripted events suspend ambient Guilt.', 'Gameplay.md §§4–5 govern behavior. Pockets must be tested with real scooter, camera and spawning.'], 19)]
    svg(key + '-combat-plan', title, parts)
    snapshot['levels'].append(level_data)


def build_boss():
    parts = [text(60, 65, 'BOKI / GAMEPLAY MOCKS V2 / ONE SHARED CLASSROOM', 20, ACID), text(60, 130, 'THE DEADLINE / RECOVERY INSIDE THE SEAL', 42, PAPER, True)]
    parts += [lines(60, 185, ['Meter-scaled proposal / same 24 × 20 m room and 14 × 12 m central clear floor.', 'Charges are on opposite riding lanes, reachable at zero battery; furniture stays outside them.'])]
    x, y, s = 100, 340, 32
    parts += [rect(x, y, 24*s, 20*s), rect(x+5*s, y+4*s, 14*s, 12*s, '#23393c', ACID, True)]
    for n in range(0,25,4):
        parts += [text(x+n*s-5,y-20,n,18,MUTED)]
    parts += [text(x+7*s,y+10*s,'CLEAR DODGE FLOOR',22,ACID), rect(x+11*s,y+2*s,2*s,s,'#6e5741',PAPER), text(x+8*s,y+1*s,'PROFESSOR / 4000 HP',20)]
    for i, (cx, cz) in enumerate(snapshot['boss']['chargePositionsXZ'], 1):
        parts += [charge(x+cx*s,y+(20-cz)*s,f'B{i}')]
    parts += [line(x+10.8*s,y+20*s,x+13.2*s,y+20*s,RED,12), text(x+5*s,y+22*s,'SOLID ENTRY SEAL / closes on entry',22,RED)]
    parts += [lines(970, 365, ['B1 / B2: equivalent +25 charges', 'Initially charged; 20 active seconds', 'to recharge after collection.', 'Leave available when battery is full.', '', 'Guilt: decorative shadow only.', 'No ambient chase or replenishment.', 'Approach encounters deactivate.', 'Approach rendering is suppressed.', '', 'Exam Barrage: 3 slow papers.', 'Deadline Slam: 0.8 s warning.', 'FERI B: two melee once at 50% HP.', 'FERI C + POWER OFF: 3400 HP.', 'No panel use: standard 4000 HP.'], 20)]
    parts += [lines(100, 1150, ['Furniture, ring radius and collision must preserve both charge approaches.', 'Battery drain continues during boss gameplay; pause freezes drain and recharge.', 'The arena door is a commitment. No return to approach routes for recovery.', 'All FERI routes reach this same arena. Guilt gains no second boss AI.', '', 'Placement and dimensions are authoring proposals. Attack and pickup rules: Gameplay.md §5 / Story.md §§3–5.'], 22)]
    svg('boss-recovery-plan', 'Professor arena with two equivalent rechargeable battery locations', parts, 1430)


def build_ui_sheet():
    parts = [text(60, 65, 'BOKI / LOCAL UI MOCKS / STATIC PRESENTATION STATES', 20, ACID), text(60, 130, 'FIGHT FOR XP. RIDE TO RECHARGE.', 44, PAPER, True)]
    parts += [lines(60, 185, ['Regular kill -> XP pack + kill count. Fixed arena charge -> +25 battery.', 'No kill battery reward. Canonical rules: Gameplay §5 / Story §5. No gameplay code here.'])]
    cards = [
        ('HUD / ACTIVE COMBAT', 'LIGHT 42 / 100', ['XP 11 / 12', 'SELECTIONS 2 / 6', 'Exit open; stay for more XP.'], ACID),
        ('BOOST / PANIC MODE OWNED', 'PANIC READY', ['Active chase within 10 m.', '+25% boost speed; +50% duration.', 'Snapshot at start; normal cooldown.'], PURPLE),
        ('XP / SIX SELECTIONS', 'MAX LEVEL', ['No XP drops or leftover collection.', 'Kills still count.', 'Keep battery recovery available.'], PURPLE),
        ('UPGRADE / 1 OF 3 TAKEN', 'RAVE OVERDRIVE', ['+30% original base laser damage.', 'Current +30% -> new +60%.', 'Capped at three selections.'], ACID),
        ('RECOVERY / BELOW CAPACITY', 'BATTERY CHARGE', ['+25 current battery.', 'Clamp to upgraded capacity.', 'Consumes one of six selections.'], ACID),
        ('RECOVERY / INJURED ONLY', 'EMERGENCY SNACK', ['Restore 30 HP; clamp to max.', 'Never duplicate this card.', 'Consumes one of six selections.'], ACID),
        ('CHARGE / AVAILABLE', 'RIDE THROUGH +25', ['Bright cell + battery silhouette.', 'Contact collects once.', 'At full battery: leave available.'], ACID),
        ('CHARGE / DEPLETED', 'RECHARGING', ['Dark cell + broken outline.', '20 active occupied-space seconds.', 'Pause/reentry retain remaining time.'], MUTED),
        ('CHARGE / RETURN CUE', 'CHARGE READY', ['Short pulse + distinct sound.', 'Return to available appearance.', 'Silhouette stays readable on Low.'], ACID),
        ('GUILT / WARNING 1.5 s', 'DON’T LOOK BACK', ['Light flicker + heartbeat.', 'Scripted events suspend ambient.', 'Time drives escalation, not upgrades.'], RED),
        ('GUILT / CHASE 6 s', 'KEEP MOVING.', ['Ordinary panic: +25% riding speed', 'for 2 s / 5 s cooldown.', 'Separate from Panic Mode boost.'], RED),
        ('EXIT / NO KILL QUOTA', 'MOVE ON WHEN READY', ['Leave without clearing a wave.', 'City C still needs side-door escape.', 'Boss entry commits; recovery inside.'], ACID),
    ]
    for n, (label, title, body, color) in enumerate(cards):
        x, y = 60 + (n % 3)*500, 290 + (n // 3)*290
        parts += [rect(x, y, 480, 255, '#121c24', color), text(x+20, y+36, label, 17, color), text(x+20, y+92, title, 25, color, True), lines(x+20, y+145, body, 17, PAPER, 32)]
    parts += [text(60, 1500, 'UPGRADE OFFER ROW: GAME PAUSED / CHOOSE EXACTLY ONE OF THREE DISTINCT ELIGIBLE OPTIONS', 21, ACID)]
    parts += [lines(60, 1560, ['The row is one example offer, not the whole pool. XP thresholds: 3 / 7 / 12 / 18 / 25 / 33.', 'Full pool: Rave Overdrive, Turbo Skiro, Bigger Battery, Double Trouble, Panic Mode, Emergency Snack.', 'Remove capped/ineligible options. Recovery fallback cards consume a selection; resolve thresholds without unpausing.', 'See ui-states.html for full eligibility, base-percentage stacking and Bigger Battery charge-percentage preservation.'], 20)]
    svg('ui-states', 'BOKI HUD, upgrade, battery and Guilt presentation states', parts, 1730)


CSS = '''*{box-sizing:border-box}body{margin:0;background:#090e14;color:#d9e1cf;font:15px/1.6 Menlo,Consolas,monospace}main{max-width:1440px;margin:auto;padding:44px 4%}h1,h2,h3{font-family:"Avenir Next Condensed","Arial Narrow",sans-serif;line-height:1.08}h1{font-size:clamp(42px,6vw,82px);margin:16px 0 28px}h2{font-size:36px}h3{font-size:24px}a{color:#d0ef75;text-underline-offset:5px}.eyebrow,.tag{color:#d0ef75;font-size:12px;letter-spacing:.09em}.muted{color:#a3b4b6}nav{display:flex;flex-wrap:wrap;gap:12px 24px;padding:24px 0;border-block:1px solid #34444e}.note{border-left:4px solid #ba86ea;background:#121c24;padding:18px 22px;margin:24px 0}section{padding:34px 0;border-bottom:1px solid #34444e;scroll-margin-top:20px}.sheet{width:100%;height:auto;display:block}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px}.card,.screen{background:#121c24;padding:24px;min-width:0}.card{border-top:3px solid #d0ef75}.screen{position:relative;min-height:220px;background:linear-gradient(155deg,#263b49,#121c24 65%)}.screen .label{color:#d0ef75;font-size:12px}.bar{height:7px;background:#263b49;margin:9px 0 18px}.bar i{display:block;height:100%;background:#d0ef75}.danger{color:#ef624a}.purple{color:#ba86ea}.state{display:inline-block;padding:5px 10px;background:#d0ef75;color:#090e14;font-weight:bold}.depleted{border:2px dashed #a3b4b6;color:#a3b4b6;background:transparent}.reward{font-size:30px;line-height:1.3}.steps{display:flex;gap:12px;flex-wrap:wrap}.steps span{padding:12px;background:#121c24}.tablewrap{overflow-x:auto}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:12px;border-bottom:1px solid #34444e}details{margin:18px 0}summary{cursor:pointer;color:#d0ef75}code{color:#ba86ea}@media(max-width:800px){.grid{grid-template-columns:1fr}main{padding:28px 5%}h2{font-size:30px}}'''

UI = '''<section id="ui"><p class="eyebrow">05 / UI STATES / STATIC MOCKS, NO GAMEPLAY CODE</p><h2>THE REWARD IS ANOTHER RISK.</h2>
<p>Keep the original compact HUD. Add these states without a new gameplay menu or a mandatory pressure meter.</p>
<div class="grid"><article class="screen"><span class="label">POHORJE / ACTIVE COMBAT SPACE</span><h3>FIGHT FOR XP.<br>RIDE TO RECHARGE.</h3><div>LIGHT 42 / 100</div><div class="bar"><i style="width:42%"></i></div><div>XP 11 / 12 · SELECTIONS 2 / 6</div><div class="bar"><i style="width:92%;background:#ba86ea"></i></div><p class="muted">Exit is open. Staying offers more XP.</p></article>
<article class="screen"><span class="label">BOOST / PANIC MODE OWNED</span><h3 class="purple">PANIC READY</h3><p>SHIFT · boost</p><p>Guilt is actively chasing within 10 m.</p><p class="muted">On activation: +25% boost speed, +50% duration. Snapshot until boost ends; normal cooldown.</p></article>
<article class="screen"><span class="label">XP / SIX SELECTIONS TAKEN</span><h3>MAX LEVEL</h3><div class="bar"><i style="width:100%;background:#ba86ea"></i></div><p>UPGRADES 6 / 6</p><p class="muted">No XP drops or leftover XP collection. Kills still count; battery comes from charges.</p></article></div>
<h3>SIGNAL FOUND / GAME PAUSED / CHOOSE ONE</h3><p class="muted">Three distinct eligible offers. Every selection consumes one slot; gameplay stays paused until the choice resolves.</p>
<div class="grid"><article class="card"><span class="tag">UPGRADE · 1 / 3 TAKEN</span><h3>RAVE OVERDRIVE</h3><p class="reward">+30% base<br>laser damage</p><p>Current +30% → after selection +60%.</p></article><article class="card"><span class="tag">RECOVERY</span><h3>BATTERY CHARGE</h3><p class="reward">+25 current<br>battery</p><p>Only offered below capacity. Clamp to capacity; consumes a selection.</p></article><article class="card"><span class="tag">RECOVERY</span><h3>EMERGENCY SNACK</h3><p class="reward">Restore<br>30 HP</p><p>Only offered while injured. One card, never duplicated as a fallback.</p></article></div>
<h3>BATTERY LOCATION / THREE READABLE STATES</h3><div class="grid"><article class="card"><span class="state">AVAILABLE +25</span><h3>RIDE THROUGH</h3><p>Bright acid cell + upright battery silhouette. Contact collects once.</p><p class="muted">Already full? The charge stays here.</p></article><article class="card"><span class="state depleted">DEPLETED</span><h3>RECHARGING</h3><p>Dark cell + broken outline. Return cue after 20 active seconds.</p><p class="muted">Timer freezes when paused or outside this space; reentry retains remaining time.</p></article><article class="card"><span class="state">CHARGE READY</span><h3>BACK ONLINE</h3><p>Short return pulse + distinct sound, then available appearance. Low settings must preserve silhouette and state.</p></article></div>
<h3>GUILT / WARNING, CHASE, EXIT</h3><div class="grid"><article class="screen"><span class="label">WARNING / 1.5 s</span><h3 class="danger">DON’T LOOK BACK</h3><p>Light flicker + heartbeat. Suspend ambient warning during authored Guilt sequences.</p></article><article class="screen"><span class="label">CHASE / 6 s</span><h3>KEEP MOVING.</h3><p>Readable shadow and audio cue; ordinary panic advantage stays +25% for 2 s / 5 s cooldown.</p></article><article class="screen"><span class="label">EXIT / NO KILL QUOTA</span><h3>MOVE ON<br>WHEN READY.</h3><p>Leave for the level exit at any time. City C still requires its scripted side-door escape.</p></article></div>
<details><summary>Full upgrade pool and eligibility</summary><p>XP thresholds: 3 → 7 → 12 → 18 → 25 → 33 cumulative packs. Roughly three selections in an ordinary run; five or six require risky stays.</p><ul><li>Rave Overdrive: +30% original base damage, cap 3.</li><li>Turbo Skiro: +20% original base riding speed, cap 2; boost speed unchanged.</li><li>Bigger Battery: +40% original base capacity, cap 2; preserve charge percentage (100 → 140 → 180 capacity).</li><li>Double Trouble: second full-damage instantaneous hit per firing cycle, cap 1; another valid target preferred, otherwise original still-living target; respect occlusion.</li><li>Panic Mode: boost modifier above, cap 1. PANIC READY requires ownership and active chase within 10 m.</li><li>Emergency Snack: repeatable while injured. Battery Charge recovery: only below capacity.</li></ul><p>Remove capped/ineligible offers. Resolve multiple thresholds sequentially without resuming combat. All bonuses add original base percentages.</p></details>
<p class="muted">The paused offer is a state example, not a live selection. No boost, charge, warning or upgrade behavior is implemented by these mocks.</p></section>'''


def build_gallery():
    page = '<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>BOKI / Gameplay mocks v2</title><style>' + CSS + '</style></head><body><main>'
    page += '<p class="eyebrow">BOKI: LATE TO FERI / LOCAL DESIGN UPDATE</p><h1>TAKE THE PATH.<br>RISK THE STAY.</h1><p>Gameplay overlays for the existing character and world concept art. Editable SVG plans make combat spaces, battery recovery and route isolation explicit.</p>'
    page += '<div class="note"><strong>Design handoff, not a Unity implementation.</strong> Canonical behavior comes from <a href="../../docs/Gameplay.md#5-loop-within-each-route">Gameplay §5</a> and <a href="../../docs/Story.md#5-upgrades-and-replay-motivation">Story §5</a>. All new spatial dimensions and marker placements are proposals. Figma is unchanged.</div>'
    page += '<nav><a href="#forest">01 POHORJE</a><a href="#city">02 MARIBOR</a><a href="#feri">03 FERI</a><a href="#boss">04 BOSS</a><a href="#ui">05 UI STATES</a><a href="#pressure">06 PRESSURE</a><a href="HANDOFF.md">Integration notes</a><a href="combat-space-plans.json">Layout data</a></nav>'
    page += '<section><h2>XP and light have different sources.</h2><div class="steps"><span>Regular kill → XP pack + kill count</span><span>XP threshold → paused choice</span><span>Fixed arena charge → +25 battery</span></div><p>Kills never recharge battery. Stay to grow stronger; time spent in combat spaces makes Guilt more dangerous. Exit without a wave-clear requirement.</p></section>'
    for key, title, *_ in LEVELS:
        page += f'<section id="{key}"><h2>{escape(title)}</h2><p><a href="sheets/{key}-combat-plan.svg">Editable combat plan SVG</a> · <a href="renders/{key}-combat-plan.png">Rendered PNG</a></p><img class="sheet" src="sheets/{key}-combat-plan.svg" alt="{key} routes with combat pockets, charge and spawn markers, entry and rear seals"></section>'
    page += '<section id="boss"><h2>THE DOOR CLOSES. RECOVERY STAYS.</h2><p><a href="sheets/boss-recovery-plan.svg">Editable boss plan SVG</a> · <a href="renders/boss-recovery-plan.png">Rendered PNG</a></p><img class="sheet" src="sheets/boss-recovery-plan.svg" alt="Shared professor arena with two equivalent charge positions and a solid entry seal"></section>'
    page += '<p><a href="sheets/ui-states.svg">Editable UI state sheet SVG</a> · <a href="renders/ui-states.png">UI state PNG</a></p>' + UI
    page += '<section id="pressure"><p class="eyebrow">06 / ENCOUNTER ANNOTATIONS / INITIAL CANONICAL TUNING</p><h2>TIME RAISES THE PRESSURE.</h2><p>20 combat-space seconds of grace → 1.5 s warning → 6 s chase → tier-based rest. Snapshot speed/damage when warning begins; snapshot rest when rest begins.</p><div class="tablewrap"><table><thead><tr><th>Accumulated active time</th><th>Rest</th><th>Chase speed</th><th>Contact damage</th></tr></thead><tbody>'
    for row in [('0–29.999 s','18 s','4.5 m/s',10),('30–59.999 s','15 s','5.0 m/s',12),('60–89.999 s','12 s','5.5 m/s',14),('90–119.999 s','9 s','6.0 m/s',16),('120 s+ / capped','6 s','6.5 m/s',18)]:
        page += '<tr>' + ''.join(f'<td>{v}</td>' for v in row) + '</tr>'
    page += '</tbody></table></div><p>Empty battery multiplies enemy speed by 1.3 afterward. Guilt contact cooldown: 1 s. Pause, upgrades, loading and scripted sequences suspend ambient time. Leaving a combat space retains clock and phase; next level resets both. Boss entry ends pursuit. Upgrades never trigger escalation.</p></section>'
    page += '<section><h2>READ WITH THE ORIGINAL ART.</h2><p><a href="../world-modeling-v1/index.html">World art and modules</a> · <a href="../character-modeling-v2/index.html">Character and rig references</a></p><p class="muted">Original PNGs remain shape/mood references. These overlays govern the added gameplay planning; canonical documents govern the rules. Validate placement, collision, camera, pacing and Windows target-hardware performance during scene integration.</p></section></main></body></html>'
    (HERE / 'index.html').write_text(page)
    (HERE / 'ui-states.html').write_text('<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>BOKI / UI states</title><style>' + CSS + '</style></head><body><main><a href="index.html">← Gameplay mock gallery</a><p><a href="sheets/ui-states.svg">Editable SVG</a> · <a href="renders/ui-states.png">PNG sheet</a></p>' + UI + '</main></body></html>')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--render', action='store_true', help='Also rasterize PNGs; requires cairosvg')
    args = parser.parse_args()
    (HERE / 'sheets').mkdir(exist_ok=True)
    (HERE / 'renders').mkdir(exist_ok=True)
    for level_args in LEVELS:
        build_level(*level_args)
    build_boss()
    build_ui_sheet()
    build_gallery()
    (HERE / 'combat-space-plans.json').write_text(json.dumps(snapshot, indent=2, ensure_ascii=False) + '\n')
    if args.render:
        import cairosvg
        for source in sorted((HERE / 'sheets').glob('*.svg')):
            cairosvg.svg2png(url=str(source), write_to=str(HERE / 'renders' / (source.stem + '.png')))
    print('Built five SVG sheets, two HTML pages and the source-hashed layout snapshot.')
