using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Die Kataloge waren uebersetzt, die Oberflaeche nicht: die Wahl am Aufzug, das Aufstiegstor,
    /// die Verbesserungs-Wahl und der ganze Endbildschirm standen auf Englisch - und neun dieser
    /// Texte lagen bereits in der Sprachtabelle, sie wurden nur nie abgerufen.
    ///
    /// Kein Katalogtest kann das finden, weil diese Texte als Zeichenketten im Quelltext stehen und
    /// nicht in einer Tabelle. Deshalb liest dieser Test die Quellen der Oberflaeche und sucht
    /// Zeichenketten, die in einen Anzeigetext muenden, ohne durch <see cref="Loc"/> zu laufen.
    ///
    /// Das ist kein Ersatz fuer Hinsehen. Es faengt genau den Fehler, der zweimal passiert ist.
    /// </summary>
    public sealed class UiTextTests
    {
        /// <summary>Aufrufe, deren Zeichenketten auf dem Bildschirm landen.</summary>
        private static readonly Regex Sinks =
            new("(CreateModal|CreateButton|CreateText|\\.text\\s*\\+?=)");

        private static readonly Regex Literal = new("\"((?:[^\"\\\\]|\\\\.)*)\"");

        /// <summary>Die Fuellstellen einer interpolierten Zeichenkette: dort steht Code, kein Text.</summary>
        private static readonly Regex Holes = new("\\{[^{}]*\\}");

        /// <summary>Drei Buchstaben in Folge. Darunter sind es Formatangaben, keine Saetze.</summary>
        private static readonly Regex Words = new("[A-Za-z]{3}");

        /// <summary>
        /// Eigennamen, Auszeichnung und Formatangaben. Bewusst kurz: jeder Eintrag hier ist ein
        /// Text, den niemand mehr uebersetzen wird.
        /// </summary>
        private static readonly string[] Allowed =
        {
            "SHATTERSPIRE", "Text", "N0", "P0", "F1", "0.##", "0.#",
            "  <size=12>", "<size=12>", "</size>"
        };

        [Test]
        public void KeinAnzeigetextUmgehtDieSprachtabelle()
        {
            var found = new List<string>();
            foreach (var folder in new[] { "UI", "Presentation" })
            {
                var root = Path.Combine(Application.dataPath, "Shatterspire", "Scripts", folder);
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                    ScanText(File.ReadAllText(file), Path.GetFileName(file), found);
            }

            Assert.That(found, Is.Empty,
                "Diese Texte landen ungefiltert auf dem Bildschirm. In Loc.T einpacken - oder, wenn "
                + "es ein Eigenname oder eine Formatangabe ist, in die Ausnahmeliste dieses Tests:\n"
                + string.Join("\n", found));
        }

        [Test]
        public void DerMelderErkenntSeineEigenenFaelle()
        {
            // Ohne diese Probe koennte der Test stumm nichts mehr pruefen, etwa weil ein
            // umbenannter Aufruf nicht mehr auf Sinks passt.
            AssertReported("scoreText.text = \"SCORE\";");
            AssertReported("CreateButton(root, \"MAIN MENU\", pos);");
            AssertClean("scoreText.text = Loc.T(\"SCORE\");");
            // Ein Ternaer innerhalb von Loc.T ist uebersetzt - beide Zweige.
            AssertClean("modal = CreateModal(Loc.T(win ? \"WON\" : \"LOST\"), sub);");
            // Interpolierte Fuellstellen enthalten Code, keinen Anzeigetext.
            AssertClean("roomText.text = $\"{Loc.T(\"FLOOR\")} {index}  ·  {Loc.Of(kind)}\";");
        }

        private static void AssertReported(string source)
        {
            var found = new List<string>();
            ScanText(source, "Probe.cs", found);
            Assert.That(found, Is.Not.Empty, "Nicht gemeldet, obwohl englisch: " + source);
        }

        private static void AssertClean(string source)
        {
            var found = new List<string>();
            ScanText(source, "Probe.cs", found);
            Assert.That(found, Is.Empty, "Falsch gemeldet: " + source + " -> " + string.Join(", ", found));
        }

        private static void ScanText(string source, string name, List<string> found)
        {
            // Anweisungsweise, damit ein ueber mehrere Zeilen verteilter Aufruf als Ganzes gesehen wird.
            foreach (var statement in source.Split(';'))
            {
                if (!Sinks.IsMatch(statement)) continue;
                var masked = MaskLocalizedCalls(statement);
                foreach (Match match in Literal.Matches(masked))
                {
                    var value = match.Groups[1].Value;
                    if (Array.IndexOf(Allowed, value) >= 0) continue;
                    // Ohne die Fuellstellen bleibt nur der feste Teil der Zeichenkette uebrig.
                    if (!Words.IsMatch(Holes.Replace(value, string.Empty))) continue;
                    found.Add($"{name}: \"{value}\"");
                }
            }
        }

        /// <summary>
        /// Ersetzt jeden Aufruf von Loc.T, Loc.Of oder Loc.Number samt seinen Argumenten. Damit
        /// zaehlt auch ein Ternaer darin als uebersetzt - beide Zweige gehen durch dieselbe Tabelle.
        /// </summary>
        private static string MaskLocalizedCalls(string statement)
        {
            var result = new StringBuilder(statement.Length);
            var index = 0;
            while (index < statement.Length)
            {
                var call = FindCall(statement, index);
                if (call < 0)
                {
                    result.Append(statement, index, statement.Length - index);
                    break;
                }
                result.Append(statement, index, call - index).Append("LOC");
                index = SkipArguments(statement, statement.IndexOf('(', call));
            }
            return result.ToString();
        }

        private static readonly string[] Calls = { "Loc.T", "Loc.Of", "Loc.Number" };

        /// <summary>Anfang des naechsten Loc-Aufrufs ab <paramref name="from"/>, sonst -1.</summary>
        private static int FindCall(string statement, int from)
        {
            var best = -1;
            foreach (var call in Calls)
            {
                var at = statement.IndexOf(call, from, StringComparison.Ordinal);
                // Nur wenn danach wirklich eine Klammer folgt - sonst ist es ein Kommentar.
                if (at < 0 || statement.IndexOf('(', at) < 0) continue;
                if (best < 0 || at < best) best = at;
            }
            return best;
        }

        /// <summary>Erste Stelle hinter der schliessenden Klammer, Zeichenketten uebersprungen.</summary>
        private static int SkipArguments(string statement, int openParen)
        {
            if (openParen < 0) return statement.Length;
            var depth = 0;
            var inString = false;
            for (var i = openParen; i < statement.Length; i++)
            {
                var c = statement[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '(') depth++;
                else if (c == ')' && --depth == 0) return i + 1;
            }
            return statement.Length;
        }
    }
}
