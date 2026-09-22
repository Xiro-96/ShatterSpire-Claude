# -*- coding: utf-8 -*-
"""Zwei Selbsttest-Laeufe nebeneinander: hat eine Aenderung die Schwierigkeit verschoben?

    python Tools/autoplay_compare.py Builds/Autoplay/20260922-0840 Builds/Autoplay/20260922-0915

Je Held: Laeufe, geschafft, erreichte Etage im Mittel, und fuer Etage 1, 2, 4 und 5 Schaden,
eigene Stuerze, ausgeteilter Schaden je Sekunde und die groessten Schadensquellen. Beide Ordner
sollten mit denselben --seeds, --repeat und --routes entstanden sein, sonst vergleicht man
Etagen, nicht Aenderungen.
"""
import glob
import json
import os
import sys

NAMES = {"Ranger": "REX", "Guardian": "BRAX", "Arcanist": "ORION", "Bomber": "KORR", "Paladin": "XIRO"}


def runs(root, hero):
    found = []
    for path in glob.glob(os.path.join(root, hero, "**", "summary.json"), recursive=True):
        found.append(json.load(open(path, encoding="utf-8")))
    return found


def mean(values):
    values = list(values)
    return sum(values) / len(values) if values else float("nan")


def floor_stats(summaries, number):
    floors = [f for s in summaries for f in s.get("floors", []) if f["floor"] == number]
    sources = {}
    for f in floors:
        for h in f.get("hits", []):
            entry = sources.setdefault(h["source"], [0, 0.0])
            entry[0] += h["hits"]
            entry[1] += h["amount"]
    top = sorted(sources.items(), key=lambda kv: -kv[1][1])[:3]
    n = max(1, len(floors))
    # Je Kampfsekunde, nicht je Etagensekunde - siehe PlaytestRecorder.FloorRecord.fightSeconds.
    dealt = [f["damageDealt"] / max(1.0, f["fightSeconds"]) for f in floors if f.get("fightSeconds")]
    return (len(floors), mean(f["damageTaken"] for f in floors), mean(f["playerDowns"] for f in floors),
            ", ".join(f"{name} {v[1] / n:.0f}" for name, v in top), mean(dealt) if dealt else None)


def describe(root, hero):
    s = runs(root, hero)
    if not s:
        return ["  keine Laeufe"]
    wins = sum(1 for x in s if "geschafft" in (x.get("endReason") or ""))
    lines = [f"  {len(s)} Laeufe, {wins} geschafft, Etage im Mittel {mean(x['floorsReached'] for x in s):.1f}, "
             f"ausgewichen {mean(x.get('dodges', 0) for x in s):.0f}, festgehangen {mean(x['stuck'] for x in s):.1f}"]
    for number in (1, 2, 4, 5):
        count, damage, downs, top, dps = floor_stats(s, number)
        if count:
            dealt = f", teilt {dps:.1f}/s im Kampf aus" if dps is not None else ""
            lines.append(f"  Etage {number} ({count}x): Schaden {damage:.0f}, gestuerzt {downs:.1f}{dealt} | {top}")
    return lines


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    before, after = sys.argv[1], sys.argv[2]
    print(f"vorher  {before}\nnachher {after}\n")
    for hero, name in NAMES.items():
        print(name)
        for label, root in (("vorher ", before), ("nachher", after)):
            for i, line in enumerate(describe(root, hero)):
                print(("  " + label if i == 0 else "         ") + line)
        print()


if __name__ == "__main__":
    main()
