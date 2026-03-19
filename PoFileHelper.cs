using Karambolo.PO;

namespace PoTranslator;

public static class PoFileHelper
{
    public static POCatalog Parse(string filePath) => ParlotPoParser.Parse(filePath);

    public static POCatalog ParseKarambolo(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var parser = new POParser();
        var result = parser.Parse(stream);

        if (!result.Success)
        {
            var diagnostics = result.Diagnostics;
            var errors = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException($"Failed to parse PO file '{filePath}': {errors}");
        }

        return result.Catalog;
    }

    public static void Write(POCatalog catalog, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var generator = new POGenerator(new POGeneratorSettings
        {
            IgnoreEncoding = true,
            IgnoreLongLines = true,
        });

        using var stream = File.Create(filePath);
        generator.Generate(stream, catalog);
    }
}
