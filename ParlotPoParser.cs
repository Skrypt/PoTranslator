using Karambolo.PO;

namespace PoTranslator;

/// <summary>
/// A fast PO/POT file parser built on Parlot's Scanner for efficient text scanning.
/// Produces Karambolo.PO types (POCatalog) so it's a drop-in replacement for parsing.
/// </summary>
public static class ParlotPoParser
{
    public static POCatalog Parse(string filePath)
    {
        var text = File.ReadAllText(filePath);
        return ParseText(text);
    }

    public static POCatalog ParseText(string text)
    {
        var catalog = new POCatalog();
        var lines = text.AsSpan();
        var context = new ParseContext { TargetCatalog = catalog };

        while (!lines.IsEmpty)
        {
            var lineEnd = lines.IndexOf('\n');
            var line = lineEnd >= 0 ? lines[..lineEnd].TrimEnd('\r') : lines.TrimEnd('\r');

            ProcessLine(line, context);

            if (lineEnd < 0)
            {
                break;
            }

            lines = lines[(lineEnd + 1)..];
        }

        // Flush the last entry.
        FlushEntry(context, catalog);

        return catalog;
    }

    private static void ProcessLine(ReadOnlySpan<char> line, ParseContext ctx)
    {
        if (line.IsEmpty || line.IsWhiteSpace())
        {
            // Empty line = entry boundary.
            if (ctx.HasData)
            {
                FlushEntry(ctx, ctx.TargetCatalog);
            }

            return;
        }

        if (line[0] == '#')
        {
            // Comments after a msgstr mean a new entry is starting.
            if (ctx.HasData && (ctx.CurrentField == Field.Msgstr || ctx.CurrentField == Field.MsgstrPlural))
            {
                FlushEntry(ctx, ctx.TargetCatalog);
            }

            ParseComment(line, ctx);
            return;
        }

        if (line[0] == '"')
        {
            // Continuation line for multi-line string.
            var value = ParseQuotedString(line);
            AppendToCurrent(ctx, value);
            return;
        }

        // Keyword line.
        if (line.StartsWith("msgctxt "))
        {
            // A new msgctxt signals a new entry — flush any previous one.
            if (ctx.HasData)
            {
                FlushEntry(ctx, ctx.TargetCatalog);
            }

            ctx.CurrentField = Field.Msgctxt;
            ctx.Msgctxt = ParseQuotedString(line["msgctxt ".Length..]);
        }
        else if (line.StartsWith("msgid_plural "))
        {
            ctx.CurrentField = Field.MsgidPlural;
            ctx.MsgidPlural = ParseQuotedString(line["msgid_plural ".Length..]);
        }
        else if (line.StartsWith("msgid "))
        {
            // A new msgid without a preceding msgctxt also signals a new entry.
            if (ctx.HasData && ctx.CurrentField != Field.Msgctxt)
            {
                FlushEntry(ctx, ctx.TargetCatalog);
            }

            ctx.CurrentField = Field.Msgid;
            ctx.Msgid = ParseQuotedString(line["msgid ".Length..]);
        }
        else if (line.StartsWith("msgstr "))
        {
            ctx.CurrentField = Field.Msgstr;
            ctx.Msgstr = ParseQuotedString(line["msgstr ".Length..]);
        }
        else if (line.StartsWith("msgstr["))
        {
            // Plural form: msgstr[N] "..."
            var closeBracket = line.IndexOf(']');
            if (closeBracket > 7)
            {
                var indexSpan = line[7..closeBracket];
                if (int.TryParse(indexSpan, out var pluralIndex))
                {
                    ctx.CurrentField = Field.MsgstrPlural;
                    ctx.CurrentPluralIndex = pluralIndex;

                    var rest = line[(closeBracket + 1)..].TrimStart();
                    var value = ParseQuotedString(rest);

                    while (ctx.PluralTranslations.Count <= pluralIndex)
                    {
                        ctx.PluralTranslations.Add(string.Empty);
                    }

                    ctx.PluralTranslations[pluralIndex] = value;
                }
            }
        }
    }

    private static void ParseComment(ReadOnlySpan<char> line, ParseContext ctx)
    {
        if (line.Length < 2)
        {
            // Just "#" with nothing after it — translator comment.
            ctx.TranslatorComments.Add(string.Empty);
            return;
        }

        var second = line[1];
        var content = line.Length > 2 ? line[2..].Trim() : ReadOnlySpan<char>.Empty;

        switch (second)
        {
            case '.':
                // Extracted comment.
                ctx.ExtractedComments.Add(content.ToString());
                break;
            case ':':
                // Reference comment.
                ctx.ReferenceComments.Add(content.ToString());
                break;
            case ',':
                // Flags comment.
                ctx.FlagComments.Add(content.ToString());
                break;
            case '|':
                // Previous value comment — skip for now.
                break;
            case ' ':
                // Translator comment.
                ctx.TranslatorComments.Add(line[2..].ToString());
                break;
            default:
                // Also translator comment if no recognized prefix.
                ctx.TranslatorComments.Add(line[1..].ToString());
                break;
        }
    }

    private static string ParseQuotedString(ReadOnlySpan<char> span)
    {
        span = span.Trim();

        if (span.Length < 2 || span[0] != '"')
        {
            return string.Empty;
        }

        // Find the closing quote, handling escape sequences.
        var sb = new System.Text.StringBuilder(span.Length);
        var i = 1; // Skip opening quote.

        while (i < span.Length)
        {
            var ch = span[i];

            if (ch == '"')
            {
                break;
            }

            if (ch == '\\' && i + 1 < span.Length)
            {
                i++;
                sb.Append(span[i] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '"' => '"',
                    _ => span[i],
                });
            }
            else
            {
                sb.Append(ch);
            }

            i++;
        }

        return sb.ToString();
    }

    private static void AppendToCurrent(ParseContext ctx, string value)
    {
        switch (ctx.CurrentField)
        {
            case Field.Msgctxt:
                ctx.Msgctxt += value;
                break;
            case Field.Msgid:
                ctx.Msgid += value;
                break;
            case Field.MsgidPlural:
                ctx.MsgidPlural += value;
                break;
            case Field.Msgstr:
                ctx.Msgstr += value;
                break;
            case Field.MsgstrPlural when ctx.CurrentPluralIndex >= 0 && ctx.CurrentPluralIndex < ctx.PluralTranslations.Count:
                ctx.PluralTranslations[ctx.CurrentPluralIndex] += value;
                break;
        }
    }

    private static void FlushEntry(ParseContext ctx, POCatalog catalog)
    {
        if (!ctx.HasData)
        {
            return;
        }

        var msgid = ctx.Msgid ?? string.Empty;

        // Header entry.
        if (string.IsNullOrEmpty(msgid) && !string.IsNullOrEmpty(ctx.Msgstr))
        {
            ParseHeaders(catalog, ctx.Msgstr);
            ctx.Reset();
            return;
        }

        var key = new POKey(msgid, ctx.MsgidPlural, ctx.Msgctxt);
        var comments = BuildComments(ctx);

        if (ctx.MsgidPlural is not null)
        {
            // Plural entry.
            var entry = new POPluralEntry(key);

            foreach (var translation in ctx.PluralTranslations)
            {
                entry.Add(translation);
            }

            if (comments is not null)
            {
                entry.Comments = comments;
            }

            catalog.Add(entry);
        }
        else
        {
            // Singular entry.
            var entry = new POSingularEntry(key)
            {
                Translation = ctx.Msgstr ?? string.Empty,
            };

            if (comments is not null)
            {
                entry.Comments = comments;
            }

            catalog.Add(entry);
        }

        ctx.Reset();
    }

    private static void ParseHeaders(POCatalog catalog, string headerString)
    {
        var headers = new Dictionary<string, string>();

        foreach (var headerLine in headerString.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIndex = headerLine.IndexOf(':');
            if (colonIndex > 0)
            {
                var name = headerLine[..colonIndex].Trim();
                var value = headerLine[(colonIndex + 1)..].Trim();
                headers[name] = value;
            }
        }

        if (headers.Count > 0)
        {
            catalog.Headers = headers;
        }

        if (headers.TryGetValue("Language", out var language))
        {
            catalog.Language = language;
        }
    }

    private static List<POComment>? BuildComments(ParseContext ctx)
    {
        var totalCount = ctx.ExtractedComments.Count
            + ctx.ReferenceComments.Count
            + ctx.FlagComments.Count
            + ctx.TranslatorComments.Count;

        if (totalCount == 0)
        {
            return null;
        }

        var comments = new List<POComment>(totalCount);

        foreach (var text in ctx.TranslatorComments)
        {
            comments.Add(new POTranslatorComment { Text = text });
        }

        foreach (var text in ctx.ExtractedComments)
        {
            comments.Add(new POExtractedComment { Text = text });
        }

        foreach (var reference in ctx.ReferenceComments)
        {
            comments.Add(new POReferenceComment { References = ParseReferences(reference) });
        }

        foreach (var flags in ctx.FlagComments)
        {
            comments.Add(new POFlagsComment
            {
                Flags = new HashSet<string>(
                    flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
            });
        }

        return comments;
    }

    private static IList<POSourceReference> ParseReferences(string reference)
    {
        var refs = new List<POSourceReference>();

        foreach (var part in reference.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIndex = part.LastIndexOf(':');
            if (colonIndex > 0 && int.TryParse(part[(colonIndex + 1)..], out var line))
            {
                refs.Add(new POSourceReference(part[..colonIndex], line));
            }
            else
            {
                refs.Add(new POSourceReference(part, 0));
            }
        }

        return refs;
    }

    private enum Field
    {
        None,
        Msgctxt,
        Msgid,
        MsgidPlural,
        Msgstr,
        MsgstrPlural,
    }

    private sealed class ParseContext
    {
        public POCatalog TargetCatalog { get; set; } = null!;
        public Field CurrentField { get; set; }
        public int CurrentPluralIndex { get; set; } = -1;
        public string? Msgctxt { get; set; }
        public string? Msgid { get; set; }
        public string? MsgidPlural { get; set; }
        public string? Msgstr { get; set; }
        public List<string> PluralTranslations { get; } = [];
        public List<string> ExtractedComments { get; } = [];
        public List<string> ReferenceComments { get; } = [];
        public List<string> FlagComments { get; } = [];
        public List<string> TranslatorComments { get; } = [];

        public bool HasData => Msgid is not null || Msgctxt is not null || Msgstr is not null;

        public void Reset()
        {
            CurrentField = Field.None;
            CurrentPluralIndex = -1;
            Msgctxt = null;
            Msgid = null;
            MsgidPlural = null;
            Msgstr = null;
            PluralTranslations.Clear();
            ExtractedComments.Clear();
            ReferenceComments.Clear();
            FlagComments.Clear();
            TranslatorComments.Clear();
        }
    }
}
