# -*- coding: utf-8 -*-
"""Kompiliert Runtime, Editor und Tests ohne laufendes Unity - in wenigen Sekunden.

Unity legt die genauen Compiler-Argumente seines letzten Imports unter
Library/Bee/artifacts/<dag>/<Assembly>.rsp ab. Dieses Skript nimmt die neueste Fassung davon,
wirft die Quelldateien und Ausgabepfade heraus, haengt den heutigen Stand der Quellen an und
baut mit Unitys eigenem Roslyn. Tests und Editor zeigen dabei auf die frisch gebaute Runtime,
nicht auf die alte in Library - sonst wuerde eine Aenderung an der Runtime ungeprueft bleiben.

Aufruf aus dem Projektordner:   python Tools/compile_check.py

Grenzen: prueft das Kompilieren, nicht das Verhalten. Fuer Tests weiter Unity im Batchmode.
Liegt in Tools/ statt im Temp-Ordner, weil der Temp-Ordner einmal geleert wurde und das
Skript damit weg war.
"""
import glob
import io
import os
import re
import subprocess
import sys
import tempfile

UNITY = os.environ.get("SHATTERSPIRE_UNITY",
                       r"C:\Program Files\Unity\Hub\Editor\6000.0.33f1\Editor")
CSC = os.path.join(UNITY, "Data", "DotNetSdkRoslyn", "csc.dll")

# Assembly -> (Name der rsp, Ordner, deren .cs heute dazugehoeren)
ASSEMBLIES = [
    ("Shatterspire.Runtime", "Shatterspire.Runtime.rsp", ["Assets/Shatterspire/Scripts"]),
    ("Assembly-CSharp-Editor", "Assembly-CSharp-Editor.rsp", ["Assets/Shatterspire/Editor"]),
    ("Shatterspire.Tests", "Shatterspire.Tests.rsp", ["Assets/Shatterspire/Tests"]),
]

RUNTIME_REF = re.compile(r'^-r:".*Shatterspire\.Runtime(\.ref)?\.dll"\s*$')


def newest_rsp(name):
    candidates = glob.glob(os.path.join("Library", "Bee", "artifacts", "*.dag", name))
    if not candidates:
        sys.exit("Keine %s gefunden - wurde das Projekt schon einmal in Unity importiert?" % name)
    return max(candidates, key=os.path.getmtime)


def sources(folders):
    files = []
    for folder in folders:
        files += glob.glob(os.path.join(folder, "**", "*.cs"), recursive=True)
    return sorted(f.replace("\\", "/") for f in files)


def build(assembly, rsp_name, folders, out_dir, runtime_dll):
    original = io.open(newest_rsp(rsp_name), encoding="utf-8-sig").read().splitlines()
    kept = []
    for line in original:
        if line.startswith('"Assets/') or line.startswith("-out:") or line.startswith("-refout:"):
            continue
        if runtime_dll and RUNTIME_REF.match(line):
            line = '-r:"%s"' % runtime_dll
        kept.append(line)
    out = os.path.join(out_dir, assembly + ".dll")
    kept.append('-out:"%s"' % out)
    files = sources(folders)
    kept += ['"%s"' % f for f in files]
    rsp = os.path.join(out_dir, assembly + ".rsp")
    io.open(rsp, "w", encoding="utf-8").write("\n".join(kept) + "\n")

    result = subprocess.run(["dotnet", CSC, "/noconfig", "@" + rsp],
                            capture_output=True, text=True, encoding="utf-8", errors="replace")
    lines = (result.stdout + result.stderr).splitlines()
    errors = [l for l in lines if ": error " in l]
    warnings = [l for l in lines if ": warning " in l and "Assets/" in l.replace("\\", "/")]
    print("== %s: %d Dateien, exit %d, %d Fehler, %d Warnungen"
          % (assembly, len(files), result.returncode, len(errors), len(warnings)))
    for line in errors[:30]:
        print("   " + line.strip())
    for line in warnings[:10]:
        print("   " + line.strip())
    return result.returncode == 0, out


def main():
    if not os.path.isdir("Library"):
        sys.exit("Bitte aus dem Projektordner aufrufen.")
    out_dir = tempfile.mkdtemp(prefix="shatterspire_compile_")
    ok = True
    runtime_dll = None
    for assembly, rsp_name, folders in ASSEMBLIES:
        built, out = build(assembly, rsp_name, folders, out_dir, runtime_dll)
        ok &= built
        if assembly == "Shatterspire.Runtime":
            if not built:
                print("Runtime baut nicht - Editor und Tests werden gegen sie nicht geprueft.")
                sys.exit(1)
            runtime_dll = os.path.abspath(out)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
