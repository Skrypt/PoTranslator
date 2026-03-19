using Karambolo.PO;
using PoTranslator.Providers;

namespace PoTranslator;

public sealed class TranslationOrchestrator
{
    private readonly ITranslationProvider _provider;
    private readonly string _targetLanguage;

    public TranslationOrchestrator(ITranslationProvider provider, string targetLanguage)
    {
        _provider = provider;
        _targetLanguage = targetLanguage;
    }

    public async Task TranslateDirectoryAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
        }

        if (!Directory.Exists(destPath))
        {
            Directory.CreateDirectory(destPath);
        }

        var files = Directory.GetFiles(sourcePath, "*.*")
            .Where(f => f.EndsWith(".po", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".pot", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (files.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No .po or .pot files found in source directory.");
            Console.ResetColor();
            return;
        }

        foreach (var sourceFile in files)
        {
            await TranslateFileAsync(sourceFile, destPath, cancellationToken);
        }
    }

    private async Task TranslateFileAsync(string sourceFile, string destPath, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourceFile) + ".po";
        var outputPath = Path.Combine(destPath, fileName);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Loading file: {Path.GetFileName(sourceFile)}");
        Console.ResetColor();

        var sourceCatalog = PoFileHelper.Parse(sourceFile);

        // Load existing translations from the output file if it exists.
        POCatalog? existingCatalog = null;
        if (File.Exists(outputPath))
        {
            try
            {
                existingCatalog = PoFileHelper.Parse(outputPath);
            }
            catch
            {
                // If existing output file can't be parsed, we'll create a fresh one.
            }
        }

        Console.WriteLine($"Output: {outputPath}");

        // Build a new output catalog with translations.
        var outputCatalog = new POCatalog
        {
            Encoding = sourceCatalog.Encoding,
            Language = _targetLanguage,
        };

        // Copy headers from existing output file, fall back to source.
        var headerSource = existingCatalog?.Headers ?? sourceCatalog.Headers;
        outputCatalog.Headers = headerSource is not null
            ? new Dictionary<string, string>(headerSource)
            : [];

        // Content-Type with charset is required by GNU gettext for correct encoding handling.
        outputCatalog.Headers["Content-Type"] = "text/plain; charset=UTF-8";

        var translatedCount = 0;
        var skippedCount = 0;

        foreach (var entry in sourceCatalog)
        {
            var msgId = entry.Key.Id;

            if (string.IsNullOrEmpty(msgId))
            {
                continue;
            }

            // Check if we already have a translation in the existing output.
            var existingTranslation = GetExistingTranslation(existingCatalog, entry.Key);

            if (!string.IsNullOrEmpty(existingTranslation))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  Already translated: {Truncate(msgId, 60)}");
                Console.ResetColor();

                var existingEntry = new POSingularEntry(entry.Key)
                {
                    Translation = existingTranslation,
                };
                CopyComments(entry,existingEntry);
                outputCatalog.Add(existingEntry);
                skippedCount++;
                continue;
            }

            try
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"  Text: {Truncate(msgId, 60)}");

                var translation = await _provider.TranslateAsync(msgId, _targetLanguage, cancellationToken);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($" -> {Truncate(translation, 60)}");
                Console.ResetColor();

                var translatedEntry = new POSingularEntry(entry.Key)
                {
                    Translation = translation,
                };
                CopyComments(entry,translatedEntry);
                outputCatalog.Add(translatedEntry);
                translatedCount++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" -> ERROR: {ex.Message}");
                Console.ResetColor();

                // Add the entry without translation so it's not lost.
                var failedEntry = new POSingularEntry(entry.Key);
                CopyComments(entry,failedEntry);
                outputCatalog.Add(failedEntry);
            }
        }

        PoFileHelper.Write(outputCatalog, outputPath);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Done: {translatedCount} translated, {skippedCount} skipped");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void CopyComments(IPOEntry source, POSingularEntry target)
    {
        if (source.Comments is null || source.Comments.Count == 0)
        {
            return;
        }

        target.Comments = [..source.Comments];
    }

    private static string? GetExistingTranslation(POCatalog? catalog, POKey key)
    {
        if (catalog is null)
        {
            return null;
        }

        if (!catalog.TryGetValue(key, out var entry))
        {
            return null;
        }

        // For singular entries, translation is at index 0.
        var translation = entry[0];
        return string.IsNullOrEmpty(translation) ? null : translation;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }
}
