# -*- coding: utf-8 -*-
"""Selbsttest: das Spiel spielt Aufstiege ohne Menschen und schreibt mit, was passiert.

Aufruf aus dem Projektordner:

    python Tools/autoplay.py --build                 # Windows-Build erzeugen, dann alle fuenf Helden
    python Tools/autoplay.py --heroes Paladin        # nur XIRO, mit dem vorhandenen Build
    python Tools/autoplay.py --floors 3 --parallel 2

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


def launch(hero, folder, args):
    os.makedirs(folder, exist_ok=True)
    command = [os.path.abspath(EXE),
               "-shatterspire-autoplay", os.path.abspath(folder),
               "-shatterspire-hero", hero,
               "-shatterspire-path", args.path,
               "-shatterspire-floors", str(args.floors),
               "-shatterspire-minutes", str(args.minutes),
               "-shatterspire-seed", str(args.seed),
               "-screen-fullscreen", "0", "-screen-width", "960", "-screen-height", "540",
               "-logFile", os.path.abspath(os.path.join(folder, "player.log"))]
    return subprocess.Popen(command)


def row(hero, folder):
    path = os.path.join(folder, "summary.json")
    if not os.path.exists(path):
        return f"{NAMES[hero]:6s} kein Protokoll - abgestuerzt oder abgebrochen, siehe player.log"
    s = json.load(open(path, encoding="utf-8"))
    return (f"{NAMES[hero]:6s} {s['floorsReached']:>2}/{s['floorsWanted']:<2} "
            f"{s['fpsMean']:5.0f}/{s['fpsLow5']:<4.0f} {s['slowShare'] * 100:5.1f} %  "
            f"{s['fastSteps']:>4} ({s['fastStepMax']:4.1f})  {s['heavyWithoutInput']:>5}  "
            f"{s['stuck']:>4}/{s['unstickDashes']:<3} {s['errors']:>4}/{s['exceptions']:<4}  {s['endReason']}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--build", action="store_true", help="vorher den Windows-Build erzeugen")
    parser.add_argument("--heroes", default=",".join(HEROES), help="kommagetrennt, z. B. Paladin,Guardian")
    parser.add_argument("--floors", type=int, default=5)
    parser.add_argument("--path", default="Brave")
    parser.add_argument("--seed", type=int, default=424242)
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
    waiting = list(heroes)
    running = {}
    deadline = args.minutes * 60 + 120
    while waiting or running:
        while waiting and len(running) < args.parallel:
            hero = waiting.pop(0)
            running[hero] = (launch(hero, os.path.join(root, hero), args), time.time())
            print(f"Start: {NAMES[hero]}", flush=True)
        for hero, (process, begun) in list(running.items()):
            if process.poll() is not None:
                print(f"Fertig: {NAMES[hero]} nach {(time.time() - begun) / 60:.1f} min", flush=True)
                del running[hero]
            elif time.time() - begun > deadline:
                process.kill()
                print(f"Abgebrochen: {NAMES[hero]} haengt laenger als {deadline / 60:.0f} min", flush=True)
                del running[hero]
        time.sleep(2)

    lines = [f"SHATTERSPIRE Selbsttest {stamp} · {args.path} · {args.floors} Etagen · Seed {args.seed}", "",
             "Held   Etage  fps Ø/5%  Zeitlupe  Schritt>Lauf  Heavy!  Fest/Dash Fehl/Ausn  Ende",
             "-" * 96]
    lines += [row(h, os.path.join(root, h)) for h in heroes]
    report = "\n".join(lines) + "\n"
    open(os.path.join(root, "report.txt"), "w", encoding="utf-8").write(report)
    print()
    print(report)
    print("Alles unter", os.path.abspath(root))


if __name__ == "__main__":
    main()
