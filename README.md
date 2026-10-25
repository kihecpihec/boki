# Boki: Late to FERI

Za GAMEJAM BEST × ŠS FERI 2026 sva kot ekipa **Bolani** ustvarila tretjeosebno akcijsko grozljivko z retro videzom PS1/VHS. V njej spremljava Bokija, ki po rejvu na Pohorju z električnim skirojem hiti skozi apokaliptični Maribor na predavanje na FERI. Z vožnjo, samodejnim laserjem, nadgradnjami in izbiro poti združujeva boj za preživetje z begom pred pošastjo Guilt.

**Ekipo sestavljava:** Ismar Mujezinović in Hanan Mešić; oba sva sodelovala pri programiranju, grafiki in zvoku.

Temo Halloween sva vključila s senčnimi pošastmi, temo in srhljivim vzdušjem, temo »It’s not a bug, it’s a feature« pa z namernimi vizualnimi motnjami, nezanesljivo uro in popačenimi prostori.

## Zagon na Windows

Za oddajo uporabljava mapo [Build/](Build/), namenjeno celotni različici Windows x86-64. **V trenutnem repozitoriju izvršne datoteke še nimava priložene; v `Build/` so za zdaj le navodila.** Uspešno izdelavo prejšnje različice sva dokumentirala v [zapisu o izdelavi](Documentation/plans/first-windows-build.md), končni preizkus na drugem računalniku Windows pa morava še opraviti.

Pripravljeno različico zaženeva tako:

1. Preneseva repozitorij oziroma celoten paket igre in ga razširiva na disk.
2. V `Build/` odpreva mapo različice Windows in zaženeva `Boki.exe`.
3. V meniju izbereva **NEW RUN**.

Ob datoteki `Boki.exe` ohraniva pripadajočo mapo `Boki_Data/`, `UnityPlayer.dll`, `MonoBleedingEdge/` in vse druge datoteke izdelane igre. Samo `.exe` ne zadostuje. Če imava priložen `Launch-Boki.cmd`, lahko z njim zaženeva igro v oknu pri 1280 × 720 z Direct3D 11.

Za ciljni računalnik sva izbrala Windows 11, Ryzen 5600G z integrirano grafiko in 16 GB RAM. Ciljava na najmanj 30 FPS pri 1280 × 720 in nizkih nastavitvah; to morava še potrditi na ciljni strojni opremi.

## Kontrole in cilj

| Vnos | Dejanje |
| --- | --- |
| WASD | Vožnja skiroja |
| Miška | Pogled oziroma kamera |
| Preslednica | Skok |
| Levi Shift | boost |
| E | Interakcija |
| Escape | Premor |
| Enter | Preskok podprtih uvodnih prizorov oziroma potrditev nadgradnje |
| Miška, A/D, puščice ali razpoložljive tipke 1–3 | Izbira nadgradnje |

Za cilj sva postavila prihod do predavalnice in zmago nad profesorjem. Laser sva zasnovala za samodejno streljanje, z izkušnjami omogočava nadgradnje, s polnilci pa obnovo baterije za luč. Poti sva povezala tako, da zgodnje izbire omejujejo poznejše možnosti. Odkrite skrivne poti omogočiva šele v naslednjem poskusu, ob smrti pa jih ohraniva.

## Dokumentacija, video in predstavitev

Podrobnejši opis koncepta, razvoja, tehnične izvedbe in uporabe AI sva zbrala v [Bolani_Boki_Dokumentacija.md](Bolani_Boki_Dokumentacija.md).

Za oddajo morava dodati še:

- **`Bolani_Boki_Video.mp4`**: 30 sekund dejanskega igranja, pokončno 1080 × 1920 (9:16), posneto z OBS Studio skladno z navodili za oddajo.
- **`Bolani_Boki_Predstavitev.pdf`** oziroma predstavitev v formatu `.ppt`: največ 4 minute za koncept, mehanike, razvoj in uporabo AI; nato imava 3 minute za demonstracijo v živo.
- **`Bolani_Boki_Predstavitev_Video.zip`**: predstavitev in video skupaj.

Videa in predstavitve v trenutnem repozitoriju še nimava priloženih.

## Uporaba AI

**Delež uporabe AI ocenjujeva na 80 %**, kar se nanaša na skupni prispevek pri izdelavi igre, predvsem pri generiranju grafike, kode in modelov. AI sva uporabila tudi pri vizualnih referencah in pripravi dokumentacije. Izvirne zvočne posnetke in evidenco njihove obdelave hraniva v [Source/sfx/](Source/sfx/README.md). Več podrobnosti navajava v [dokumentaciji](Bolani_Boki_Dokumentacija.md#6-uporaba-ai-in-zunanjih-virov).

## Struktura in odpiranje projekta

Repozitorij sva uredila tako:

- [Build/](Build/): mesto za izdelano igro Windows in vse pripadajoče datoteke.
- [Documentation/](Documentation/): načrti, pravila in zapisi preverjanj.
- `Source/Game/`: projekt Unity.
- `Source/music/`, `Source/sfx/`, `Source/output/` in `Source/tools/`: izvorni zvok, modeli, reference in orodja za pripravo vsebin.

Za odpiranje projekta v Unity Hub izbereva **`Source/Game/`** in Unity **6000.6.7f1**, kot sva določila v `Source/Game/ProjectSettings/ProjectVersion.txt`. Po uvozu paketov in vsebin odpreva `Assets/Scenes/MainMenu.unity` ter pritisneva Play. Za izdelavo različice Windows potrebujeva še Windows Build Support (Mono); postopek opisujeva v [navodilih za izdelavo](Documentation/plans/v2-windows-arm64-build.md).

Pri razvoju uporabljava C#, Universal Render Pipeline in Unity Input System. Podrobnosti hraniva v dokumentih [Pravila igranja](Documentation/Gameplay.md), [Zgodba](Documentation/Story.md) in [Arhitektura](Documentation/Architecture.md), pravila sodelovanja pa v [AGENTS.md](AGENTS.md).
