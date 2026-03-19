using System.Diagnostics;
using Karambolo.PO;

namespace PoTranslator;

public static class ParserBenchmark
{
    public static void Run(string sourcePath, int iterations = 100)
    {
        var files = Directory.GetFiles(sourcePath, "*.*")
            .Where(f => f.EndsWith(".po", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".pot", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (files.Length == 0)
        {
            Console.WriteLine("No .po or .pot files found.");
            return;
        }

        var totalEntries = 0;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Benchmarking {files.Length} file(s), {iterations} iterations each");
        Console.WriteLine(new string('-', 80));
        Console.ResetColor();

        // Warmup both parsers.
        foreach (var file in files)
        {
            PoFileHelper.ParseKarambolo(file);
            ParlotPoParser.Parse(file);
        }

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);

            // Benchmark Karambolo.
            var sw = Stopwatch.StartNew();
            POCatalog? karamboloCatalog = null;
            for (var i = 0; i < iterations; i++)
            {
                karamboloCatalog = PoFileHelper.ParseKarambolo(file);
            }

            sw.Stop();
            var karamboloMs = sw.Elapsed.TotalMilliseconds;
            var karamboloCount = karamboloCatalog!.Count;

            // Benchmark Parlot.
            sw.Restart();
            POCatalog? parlotCatalog = null;
            for (var i = 0; i < iterations; i++)
            {
                parlotCatalog = ParlotPoParser.Parse(file);
            }

            sw.Stop();
            var parlotMs = sw.Elapsed.TotalMilliseconds;
            var parlotCount = parlotCatalog!.Count;

            totalEntries += karamboloCount;

            var speedup = karamboloMs / parlotMs;
            var faster = speedup > 1 ? "Parlot" : "Karambolo";

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\n  {fileName} ({karamboloCount} entries)");
            Console.ResetColor();

            Console.WriteLine($"    Karambolo: {karamboloMs,8:F2} ms ({karamboloMs / iterations:F3} ms/iter)");
            Console.WriteLine($"    Parlot:    {parlotMs,8:F2} ms ({parlotMs / iterations:F3} ms/iter)");

            Console.ForegroundColor = speedup > 1 ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine($"    Winner:    {faster} ({Math.Max(speedup, 1 / speedup):F2}x faster)");
            Console.ResetColor();

            // Verify correctness.
            if (karamboloCount != parlotCount)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    MISMATCH: Karambolo={karamboloCount} entries, Parlot={parlotCount} entries");
                Console.ResetColor();
            }

            VerifyEntries(karamboloCatalog!, parlotCatalog!, fileName);
        }

        // Run aggregate benchmark.
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n{new string('-', 80)}");
        Console.WriteLine($"Aggregate: all {files.Length} files x {iterations} iterations");
        Console.ResetColor();

        var allSw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            foreach (var file in files)
            {
                PoFileHelper.ParseKarambolo(file);
            }
        }

        allSw.Stop();
        var totalKarambolo = allSw.Elapsed.TotalMilliseconds;

        allSw.Restart();
        for (var i = 0; i < iterations; i++)
        {
            foreach (var file in files)
            {
                ParlotPoParser.Parse(file);
            }
        }

        allSw.Stop();
        var totalParlot = allSw.Elapsed.TotalMilliseconds;

        var totalSpeedup = totalKarambolo / totalParlot;

        Console.WriteLine($"  Karambolo total: {totalKarambolo,8:F2} ms");
        Console.WriteLine($"  Parlot total:    {totalParlot,8:F2} ms");

        Console.ForegroundColor = totalSpeedup > 1 ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"  Overall:         {(totalSpeedup > 1 ? "Parlot" : "Karambolo")} is {Math.Max(totalSpeedup, 1 / totalSpeedup):F2}x faster");
        Console.ResetColor();

        Console.WriteLine($"\n  Total entries parsed: {totalEntries}");
    }

    private static void VerifyEntries(POCatalog karambolo, POCatalog parlot, string fileName)
    {
        foreach (var entry in karambolo)
        {
            if (string.IsNullOrEmpty(entry.Key.Id))
            {
                continue;
            }

            if (!parlot.Contains(entry.Key))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    MISSING in Parlot: {entry.Key.Id}");
                Console.ResetColor();
                continue;
            }

            var parlotEntry = parlot[entry.Key];
            var karamboloTranslation = entry[0] ?? string.Empty;
            var parlotTranslation = parlotEntry[0] ?? string.Empty;

            if (karamboloTranslation != parlotTranslation)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    TRANSLATION MISMATCH for '{entry.Key.Id}':");
                Console.WriteLine($"      Karambolo: {karamboloTranslation}");
                Console.WriteLine($"      Parlot:    {parlotTranslation}");
                Console.ResetColor();
            }
        }
    }
}
