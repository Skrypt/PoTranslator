namespace PoTranslator.Providers;

public interface ITranslationProvider
{
    Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default);
}
