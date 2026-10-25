# Bolani — Boki: Late to FERI

GAMEJAM BEST × ŠS FERI 2026 · Dokumentacija projekta

## I. Kratek povzetek

### 1. Podatki o ekipi

**Ime ekipe:** Bolani
**Člani in vloge:** Ismar Mujezinović – programiranje, grafika, zvok; Hanan Mešić – programiranje, grafika, zvok.

### 2. Ime igre

**Boki: Late to FERI**
*Predavanje ne bo čakalo. Tudi Guilt ne.*

### 3. Povzetek koncepta

V igri spremljava Bokija, ki se po rejvu na Pohorju prebudi in se z električnim skirojem odpravi skozi apokaliptični Maribor, da bi pravočasno prišel na predavanje na FERI. Igralcu omogočava izmikanje sovražnikom, zbiranje izkušenj in baterijskih polnilcev ter izbiro poti med zasledovanjem pošasti Guilt. Temo Halloween sva izrazila skozi senčne pošasti, temo in srhljivo retro vzdušje. Temo »It’s not a bug, it’s a feature« sva vključila z namernimi motnjami VHS, nezanesljivo uro in popačenimi prostori, ki so del nočne more.

### 4. Ključne mehanike

- Tretjeosebna vožnja skiroja, skok in boost ter samodejno streljanje z laserjem.
- Baterija omogoča osvetlitev; polnilci jo obnavljajo, ubijanje pa prinaša izkušnje.
- Izbira nadgradenj ob vse nevarnejšem boju in stopnjevanju pritiska pošasti Guilt.
- Povezane izbire poti skozi Pohorje, Maribor in FERI; skrivne poti se odklepajo za naslednje poskuse.
- Zaključni spopad s profesorjem, smrt in ponovni poskus ter razkritje sobotnega preobrata.

### 5. Tehnologije in orodja

Uporabljala sva Unity **6000.6.7f1**, C#, Universal Render Pipeline, Unity Input System, Blender, Git, Python in FFmpeg. Pri pripravi dokumentacije sva uporabila Codex, pri vizualnih referencah pa ustvarjanje slik z AI.

### 6. Glavni poudarki

Poti sva povezala tako, da zgodnje izbire omejujejo poznejše možnosti, trajna odkritja pa spreminjajo naslednje poskuse. Z daljšim zadrževanjem v boju ponujava več nadgradenj, hkrati pa stopnjujeva nevarnost pošasti Guilt in odločanje med nagrado in pobegom.

### 7. Uporaba AI in zunanjih virov

**Ocenjeni delež uporabe AI: 80 %.** S tem ocenjujeva skupni prispevek AI pri izdelavi igre, predvsem pri generiranju grafike, kode in modelov. AI sva uporabila tudi za vizualne reference in pomoč pri dokumentaciji. V repozitoriju hraniva tudi izvirne zvočne posnetke.

### 8. Status izdelka

V repozitorij sva vključila povezane ravni, menije, boj, nadgradnje, shranjevanje odkritih poti, zaključni spopad in konec igre. Zabeležila sva uspešno izdelavo različice Windows x64. Preveriti morava še končno igranje na Windows, sliko in zvok, uravnoteženje ter zmogljivost na Ryzen 5600G. Ciljava na najmanj 30 FPS pri 1280 × 720 in nizkih nastavitvah.

## II. Razširjeni del

### 1. Razvojni proces

Projekt sva načrtovala okoli igralne poti od Bellevueja prek Pohorja in Maribora do predavalnice na FERI. Ločeno sva dokumentirala [zgodbo](Documentation/Story.md), [pravila igranja](Documentation/Gameplay.md) in [arhitekturo](Documentation/Architecture.md). Načrte mejnikov in zapise preverjanj hraniva v `Documentation/plans/`.

Oba sva sodelovala pri programiranju, grafiki in zvoku. Pri razvoju uporabljava ločene veje za funkcionalnosti, preizkusne scene, ponovno uporabo modelov, skriptirano pripravo vsebin in preverjanje v Unity pred vključitvijo v igro.

### 2. Podrobnosti o mehanikah

Za cilj sva postavila prihod do predavalnice in zmago nad profesorjem. Na vsaki ravni ponujava poti A/B/C, pri čemer s prejšnjimi izbirami omejujeva naslednje možnosti. Odkritja poti C shraniva takoj, njihovo uporabo pa omogočiva šele v naslednjem poskusu. Ob smrti ponastaviva trenutno zdravje, izkušnje, nadgradnje in izbire poti, ohraniva pa trajna odkritja.

Laser sva zasnovala za samodejno napadanje. Običajnim sovražnikom sva dodala zasledovanje ali streljanje izstrelkov, pošast Guilt pa sva zasnovala kot neranljivo grožnjo, ki jo stopnjujeva z aktivnim časom v boju. Za izhod ne zahtevava določenega števila premaganih sovražnikov. S polnilci omogočava obnovo energije za luč, z izkušnjami pa nadgradnje. Profesorju sva dodala papirnate izstrelke in vnaprej označene napade na tla. Na servisno pot sva vključila panel `POWER OFF`, s katerim omogočava zmanjšanje njegovega začetnega zdravja.

| Tipka oziroma vnos | Dejanje |
| --- | --- |
| WASD / miška | Vožnja / pogled |
| Preslednica / levi Shift | Skok / boost |
| E / Escape | Interakcija / premor |
| Enter | Preskok podprtih režiranih prizorov; potrditev nadgradnje |
| Miška, A/D ali puščice; razpoložljive tipke 1–3 | Izbira nadgradnje |

S srhljivim vzdušjem in namernimi motnjami ohranjava negotovost, ali je Bokijeva pot resnična. Za uspešen poskus predvidevava približno 7–12 minut; trajanje morava še preveriti z igranjem.

### 3. Tehnična izvedba

Projekt Unity hraniva v `Source/Game/`, sisteme igre v `Source/Game/Assets/Scripts/`, orodja za pripravo in preverjanje v `Source/Game/Assets/Editor/`, scene pa v `Source/Game/Assets/Scenes/`. Deterministična pravila sva ločila od komponent Unity, da lahko s testi EditMode preverjava omejitve poti, napredovanje in časovne mehanike.

Za začetno sceno sva izbrala `MainMenu`, nato vključila uvodno besedilo ter `V1_Pohorje`, `V1_Maribor` in `V1_FERI`. S skupnim stanjem poskusa med ravnmi prenašava zdravje, baterijo in nadgradnje. Z osrednjim sistemom usklajujeva ustavitev igre med premorom, izbiro nadgradenj in režiranimi prizori. Obremenitev integrirane grafike zmanjšujeva s ponovno uporabo sovražnikov, preprostimi trkalnimi telesi, nizkopoligonskimi modeli in omejeno osvetlitvijo. Vpliv naraščajočega števila sovražnikov morava še izmeriti na ciljni strojni opremi.

### 4. Vizualni in zvočni del

V vizualnem slogu združujeva nizkopoligonske oblike obdobja PS1 in zgodnjega PS2, teksture nizke ločljivosti, meglo, temo in motnje VHS. Izvorne modele Blender, izvoze FBX in referenčne pakete hraniva v `Source/output/`, uvožene vsebine igre pa v `Source/Game/Assets/`.

Reference likov in okolij, ustvarjene z AI, sva opisala v [paketu likov](Source/output/character-modeling-v2/README.md) in [paketu okolij](Source/output/world-modeling-v1/README.md). Te slike uporabljava kot reference, zato jih vseh ne štejeva med teksture, prikazane v igri. Pri modelih in animacijah uporabljava tudi skriptirane postopke izdelave in izvoza.

Izvirne glasovne posnetke, zvočne učinke in evidenco obdelave hraniva v [Source/sfx/](Source/sfx/README.md). FFmpeg sva uporabila za pretvorbo, rezanje in postopne prehode glasnosti, kopije za igro pa sva uvozila v Unity.

### 5. Namestitev in zagon igre na Windows

1. Preneseva in v mapo razširiva celoten arhiv različice za Windows.
2. Skupaj ohraniva `Boki.exe`, `Boki_Data/`, `UnityPlayer.dll` in vse druge priložene datoteke za delovanje igre. Sama datoteka `.exe` ne zadostuje.
3. Zaženeva `Boki.exe` in izbereva **NEW RUN**. Če imava v paketu `Launch-Boki.cmd`, lahko z njim igro zaženeva v oknu pri 1280 × 720 z Direct3D 11.

Izdelano igro shranjujeva v `Source/Game/Builds/Windows-x64/Boki.exe`; izdelanih različic ne vključujeva v Git. Za ciljno konfiguracijo sva izbrala Windows 11 z Ryzen 5600G in 16 GB RAM; minimalnih zahtev še nisva izmerila.

Izvorni projekt odpreva takole:

1. Klonirava [repozitorij](https://github.com/kihecpihec/boki) ali razširiva arhiv izvorne kode.
2. Prek Unity Hub namestiva Unity **6000.6.7f1** ter podporo Windows Build Support za izdelavo različice Windows.
3. Dodava in odpreva mapo `Source/Game/` ter počakava na obnovitev paketov in uvoz vsebin.
4. Odpreva `Assets/Scenes/MainMenu.unity` in pritisneva Play. Za izdelavo igre izbereva Windows x86_64 in ohraniva nastavljeni seznam scen.

Postopek izdelave in omejitve preverjanja sva opisala v [zapisu o različici Windows](Documentation/plans/first-windows-build.md).

### 6. Uporaba AI in zunanjih virov

**Skupni delež uporabe AI ocenjujeva na 80 %.** Ocena se nanaša na skupni prispevek AI pri izdelavi igre, predvsem na grafiko, kodo in modele.

| Področje | Uporaba |
| --- | --- |
| Grafika | Z AI sva generirala grafiko in vizualne reference likov ter okolij. |
| Koda | Z AI sva generirala programsko kodo. |
| Modeli | Z AI sva generirala modele; postopke priprave in izvoza hraniva v `Source/output/`. |
| Dokumentacija | S Codexom sva si pomagala pri pripravi besedila na podlagi repozitorija. |
| Zvok | Izvirne posnetke in izvor izsekov hraniva v `Source/sfx/manifest.json`; za obdelavo sva uporabila FFmpeg. |

V repozitoriju beleživa preverjanje modelov in izvozov, uvoz v Unity, avtomatizirane teste pravil ter osnovne preizkuse delovanja.

### 7. Testiranje

V [zapisu preverjanja menijev](Documentation/plans/main-menu-flow-verification.md) sva zabeležila 178 uspešnih testov EditMode in preizkuse poteka igre brez grafičnega vmesnika. V [zapisu o različici Windows](Documentation/plans/first-windows-build.md) sva zabeležila uspešno izdelavo x64 in preverjanje arhiva, zagona ter igranja na Windows pa še nisva preverila. Pri dokumentaciji se opirava na obstoječe zapise; testov zanjo nisva ponovno izvedla.

Preveriti morava še celotno igranje na Windows, tipkovnico in miško, čas odklepanja poti, premor in nadaljevanje, zvočno ravnovesje ter zmogljivost med množicami sovražnikov in bojem s profesorjem.

### 8. Načrti za naprej

Prednost bova dala meritvam na ciljni strojni opremi, uravnoteženju težavnosti in trajanja poskusa ter končni vizualni in zvočni dodelavi.

## III. Kratek demo video

Za oddajo morava priložiti **30 sekund dejanskega igranja**, v formatu **MP4**, pokončno **1080 × 1920 (9:16)**. Za snemanje lahko uporabiva OBS Studio ali drug snemalnik, ki podpira ta format. V posnetku lahko prikaževa vožnjo skiroja, boj in nadgradnje ter pošast Guilt ali profesorja.
