# -*- coding: utf-8 -*-
"""Selbsttest: das Spiel spielt Aufstiege ohne Menschen und schreibt mit, was passiert.

Aufruf aus dem Projektordner:

    python Tools/autoplay.py --build                 # Windows-Build erzeugen, dann alle fuenf Helden
    python Tools/autoplay.py --heroes Paladin        # nur XIRO, mit dem vorhandenen Build
    python Tools/autoplay.py --floors 3 --parallel 2
    python Tools/autoplay.py --repeat 3 --seeds 424242,7,99   # jeder Held 3x je Seed

Ein einzelner Lauf sagt wenig ueber die Schwierigkeit: am 21.09. nahm BRAX auf Etage 1 mit
demselben Seed einmal 176, einmal 216 und einmal 349 Schaden. Fuer Balancefragen also --repeat.

Jeder Held laeuft in einem eigenen Fenster (960x540, stumm). Am Ende liegt unter
Builds/Autoplay/<Zeitstempel>/ je Held ein Ordner mit summary.txt, summary.json, events.log,
player.log und Bildern - und daneben report.txt mit allen Helden auf einem Blatt.

Der Build braucht ein geschlossenes Unity (sonst ist das Projekt gesperrt). Die Laeufe selbst
nicht. Der Selbsttest fasst keinen Spielstand an - siehe MetaSaveSystem.Ephemeral.
"""
import argparse
import datetime
import json
import os
import subprocess
import sys
import time

UNITY = os.environ.get("SHATTERSPIRE_UNITY",
                       r"C:\Program Files\Unity\Hub\Editor\6000.0.33f1\Editor\Unity.exe")
EXE = os.path.join("Builds", "WindowsCapture", "Shatterspire.exe")
HEROES = ["Ranger", "Guardian", "Arcanist", "Bomber", "Paladin"]
NAMES = {"Ranger": "REX", "Guardian": "BRAX", "Arcanist": "ORION", "Bomber": "KORR", "Paladin": "XIRO"}


def build():
    if os.path.exists(os.path.join("Temp", "UnityLockfile")):
        sys.exit("Unity hat das Projekt offen - fuer den Build bitte schliessen.")
    log = os.path.abspath(os.path.join("Builds", "autoplay_build.log"))
    os.makedirs("Builds", exist_ok=True)
    print("Baue den Windows-Build ...", flush=True)
    result = subprocess.run([UNITY, "-batchmode", "-quit", "-nographics", "-projectPath", os.getcwd(),
                             "-buildTarget", "Win64",
                             "-executeMethod", "Shatterspire.Editor.AndroidBuild.BuildWindowsCapture",
                             "-logFile", log])
    if result.returncode != 0 or not os.path.exists(EXE):
        sys.exit("Build fehlgeschlagen, siehe " + log)
    print("Build fertig:", EXE, flush=True)


def launch(hero, folder, args, seed):
    os.makedirs(folder, exist_ok=True)
    command = [os.path.abspath(EXE),
               "-shatterspire-autoplay", os.path.abspath(folder),
               "-shatterspire-hero", hero,
               "-shatterspire-path", args.path,
               "-shatterspire-floors", str(args.floors),
               "-shatterspire-minutes", str(args.minutes),
               "-shatterspire-seed", str(seed),
               "-shatterspire-routes", args.routes,
               "-screen-fullscreen", "0", "-screen-width", "960", "-screen-height", "540",
               "-logFile", os.path.abspath(os.path.join(folder, "player.log"))]
    return subprocess.Popen(command)


def load(folder):
    path = os.path.join(folder, "summary.json")
    return json.load(open(path, encoding="utf-8")) if os.path.exists(path) else None


def row(hero, folder, label):
    s = load(folder)
    if s is None:
        return f"{label:12s} kein Protokoll - abgestuerzt oder abgebrochen, siehe player.log"
    return (f"{label:12s} {s['floorsReached']:>2}/{s['floorsWanted']:<2} "
            f"{s['fpsMean']:5.0f}/{s['fpsLow5']:<4.0f} {s['slowShare'] * 100:5.1f} %  "
            f"{s['fastSteps']:>4} ({s['fastStepMax']:4.1f})  {s['heavyWithoutInput']:>5}  "
            f"{s['stuck']:>4}/{s['unstickDashes']:<3} {s['errors']:>4}/{s['exceptions']:<4}  {s['endReason']}")


def mean(values):
    return sum(values) / len(values) if values else 0.0


def aggregate(hero, folders):
    """Eine Zeile je Held ueber alle Wiederholungen, und woher der Schaden auf Etage 1 kam."""
    runs = [s for s in (load(f) for f in folders) if s]
    if not runs:
        return [f"{NAMES[hero]:6s} keine Protokolle"]
    wins = sum(1 for s in runs if "geschafft" in (s.get("endReason") or ""))
    first = [s["floors"][0] for s in runs if s.get("floors")]
    lines = [f"{NAMES[hero]:6s} {len(runs)} Laeufe, {wins} geschafft, Etage im Mittel {mean([s['floorsReached'] for s in runs]):.1f}"
             f" | Etage 1: Schaden {mean([f['damageTaken'] for f in first]):.0f}"
             f", selbst gefallen {mean([f['playerDowns'] for f in first]):.1f}"
             f", tiefstes Leben {mean([f['lowestHealth'] for f in first]) * 100:.0f} %"
             f", Dauer {mean([f['seconds'] for f in first]):.0f} s"]
    sources = {}
    for f in first:
        for h in f.get("hits", []):
            entry = sources.setdefault(h["source"], [0, 0.0, 0])
            entry[0] += h["hits"]
            entry[1] += h["amount"]
            entry[2] += h.get("afterDodge", 0)
    if sources:
        parts = sorted(sources.items(), key=lambda kv: -kv[1][1])
        n = max(1, len(first))
        lines.append("         Quellen je Lauf: " + ", ".join(
            f"{name} {v[0] / n:.1f}x/{v[1] / n:.0f}" + (f" ({v[2] / n:.1f} n.A.)" if v[2] else "")
            for name, v in parts))
    return lines


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--build", action="store_true", help="vorher den Windows-Build erzeugen")
    parser.add_argument("--heroes", default=",".join(HEROES), help="kommagetrennt, z. B. Paladin,Guardian")
    parser.add_argument("--floors", type=int, default=5)
    parser.add_argument("--path", default="Brave")
    parser.add_argument("--seed", type=int, default=424242)
    parser.add_argument("--seeds", default="", help="kommagetrennt; ersetzt --seed")
    parser.add_argument("--repeat", type=int, default=1, help="Laeufe je Held und Seed")
    parser.add_argument("--routes", default="cycle", choices=["cycle", "safe", "risky"],
                        help="Routenwahl: im Wechsel, vorsichtig (Schatz/Kampf) oder gierig (Elite)")
    parser.add_argument("--minutes", type=float, default=12)
    parser.add_argument("--parallel", type=int, default=3)
    args = parser.parse_args()

    if not os.path.isdir("Assets"):
        sys.exit("Bitte aus dem Projektordner aufrufen.")
    if args.build or not os.path.exists(EXE):
        build()

    heroes = [h.strip() for h in args.heroes.split(",") if h.strip()]
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M")
    root = os.path.join("Builds", "Autoplay", stamp)
    seeds = [int(x) for x in args.seeds.split(",") if x.strip()] or [args.seed]
    single = len(seeds) == 1 and args.repeat <= 1
    jobs = []
    for seed in seeds:
        for n in range(1, max(1, args.repeat) + 1):
            for hero in heroes:
                folder = os.path.join(root, hero) if single else os.path.join(root, hero, f"{seed}-{n}")
                label = NAMES[hero] if single else f"{NAMES[hero]} {seed}-{n}"
                jobs.append((hero, seed, folder, label))
    waiting = list(jobs)
    running = {}
    deadline = args.minutes * 60 + 120
    while waiting or running:
        while waiting and len(running) < args.parallel:
            hero, seed, folder, label = waiting.pop(0)
            running[label] = (launch(hero, folder, args, seed), time.time())
            print(f"Start: {label}", flush=True)
        for label, (process, begun) in list(running.items()):
            if process.poll() is not None:
                print(f"Fertig: {label} nach {(time.time() - begun) / 60:.1f} min", flush=True)
                del running[label]
            elif time.time() - begun > deadline:
                process.kill()
                print(f"Abgebrochen: {label} haengt laenger als {deadline / 60:.0f} min", flush=True)
                del running[label]
        time.sleep(2)

    seed_text = ", ".join(str(x) for x in seeds)
    lines = [f"SHATTERSPIRE Selbsttest {stamp} · {args.path} · {args.floors} Etagen · Seed {seed_text}"
             + (f" · je {args.repeat}x" if args.repeat > 1 else "") + f" · Routen {args.routes}", "",
             "Lauf         Etage  fps Ø/5%  Zeitlupe  Schritt>Lauf  Heavy!  Fest/Dash Fehl/Ausn  Ende",
             "-" * 102]
    lines += [row(hero, folder, label) for hero, seed, folder, label in jobs]
    # Welche Etagen die Seeds tatsaechlich hatten. Am 22.09. trugen drei Seeds hintereinander
    # Overload auf Etage 4, und "Etage 4 ist die Wand" war zum Teil nur das.
    lines += ["", "Etagen je Seed (Art/Anomalie, aus dem ersten Lauf)"]
    for seed in seeds:
        first = next((load(folder) for _, sd, folder, _ in jobs if sd == seed and load(folder)), None)
        if first:
            lines.append(f"  {seed:>8}: " + "  ".join(
                f"E{f['floor']} {f['kind']}/{f.get('anomaly') or '-'}" for f in first.get("floors", [])))
    if not single:
        lines += ["", "Je Held"]
        for hero in heroes:
            lines += aggregate(hero, [folder for h, _, folder, _ in jobs if h == hero])
    report = "\n".join(lines) + "\n"
    open(os.path.join(root, "report.txt"), "w", encoding="utf-8").write(report)
    print()
    print(report)
    print("Alles unter", os.path.abspath(root))


if __name__ == "__main__":
    main()
