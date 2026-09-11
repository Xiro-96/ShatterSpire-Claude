# Shatterspire Test Runner

Führt die EditMode-Tests **ohne Unity** aus. Nicht als Ersatz für den Test Runner im
Editor gedacht, sondern für die schnelle Runde beim Entwickeln: Unity braucht für
Import, Domain-Reload und Testlauf leicht eine Minute, das hier läuft in zwei Sekunden.

Funktioniert für alles, was reine Rechnung ist — `ClimbScore`, `RankTable`,
`ShiftCalendar`, `PerkCatalog`, `RunConfig`. Tests, die `Resources.Load`, `GameObject`
oder `UnityEngine.Random` anfassen, scheitern hier mit
`ECall methods must be packaged into a system module`. Das ist keine Fehlfunktion,
sondern die Grenze: diese Aufrufe gehen in Unitys nativen Kern und existieren
außerhalb des Editors nicht. Solche Tests gehören in den Editor.

## Benutzen

Erst die Assemblies mit Unitys eigenem Roslyn bauen, dann laufen lassen. Die
Response-Files aus dem letzten Unity-Import liefern die exakten Compiler-Argumente:

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.0.33f1/Editor/Data"
DAG=$(ls -d Library/Bee/artifacts/*.dag | head -1)
OUT=/tmp/shatterspire-check && mkdir -p "$OUT"

# Runtime und Tests neu bauen, mit aktueller Quelldateiliste
for pair in "Shatterspire.Runtime Assets/Shatterspire/Scripts" "Shatterspire.Tests Assets/Shatterspire/Tests"; do
  set -- $pair
  grep -v -E '^-out:|^-refout:|^"Assets/.*\.cs"$' "$DAG/$1.rsp" \
    | sed "s|$DAG/Shatterspire.Runtime.ref.dll|$OUT/Shatterspire.Runtime.dll|" > "$OUT/$1.rsp"
  echo "-out:\"$OUT/$1.dll\"" >> "$OUT/$1.rsp"
  find $2 -name '*.cs' -printf '"%p"\n' >> "$OUT/$1.rsp"
  dotnet "$UNITY/DotNetSdkRoslyn/csc.dll" "@$OUT/$1.rsp"
done

dotnet run --project Tools/TestRunner -- "$OUT/Shatterspire.Tests.dll" \
  "$OUT" "$UNITY/Managed/UnityEngine" \
  Library/PackageCache/com.unity.ext.nunit/net40/unity-custom \
  Library/ScriptAssemblies
```

Erstes Argument ist die Test-Assembly, alle weiteren sind Suchpfade für abhängige
DLLs. Rückgabewert 0 heißt: kein Test fehlgeschlagen.

## Warum es das gibt

Ein stummer Angriff hat zwei Entwicklungsrunden gekostet: der Animations-Driver suchte
Clips, die es nicht gab, `FindClip` gab `null` zurück, und niemand konnte es merken.
Kompilieren allein beweist zu wenig. Alles, was sich ohne Szene entscheiden lässt,
sollte auch ohne Szene nachweisbar sein.
