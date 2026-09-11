using System;
using System.Linq;
using System.Reflection;

// Laedt die von Unitys Roslyn gebauten Assemblies und ruft alle [Test]-Methoden
// direkt auf. Reicht fuer reine Rechenlogik ohne Szene.
class Program
{
    static int Main(string[] args)
    {
        var probeDirs = args.Skip(1).ToArray();
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            var name = new AssemblyName(e.Name).Name + ".dll";
            foreach (var dir in probeDirs)
            {
                var path = System.IO.Path.Combine(dir, name);
                if (System.IO.File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };

        var asm = Assembly.LoadFrom(args[0]);
        int pass = 0, fail = 0, skip = 0;
        foreach (var type in asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract))
        {
            var tests = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute"))
                .ToArray();
            if (tests.Length == 0) continue;
            Console.WriteLine($"\n== {type.Name} ==");
            object instance;
            try { instance = Activator.CreateInstance(type); }
            catch (Exception ex) { Console.WriteLine($"  !! ctor: {ex.Message}"); fail += tests.Length; continue; }

            foreach (var test in tests)
            {
                try
                {
                    test.Invoke(instance, null);
                    Console.WriteLine($"  PASS  {test.Name}");
                    pass++;
                }
                catch (TargetInvocationException tie)
                {
                    var inner = tie.InnerException;
                    if (inner != null && inner.GetType().Name == "IgnoreException")
                    {
                        Console.WriteLine($"  SKIP  {test.Name}  ({inner.Message})");
                        skip++;
                        continue;
                    }
                    Console.WriteLine($"  FAIL  {test.Name}");
                    Console.WriteLine("        " + (inner?.Message ?? "?").Replace("\n", "\n        "));
                    fail++;
                }
            }
        }
        Console.WriteLine($"\n---- {pass} bestanden, {fail} fehlgeschlagen, {skip} uebersprungen ----");
        return fail == 0 ? 0 : 1;
    }
}
