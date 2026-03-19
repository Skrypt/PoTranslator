using Google.Cloud.Translation.V2;

namespace PoTranslator.Providers;

public sealed class GoogleTranslateProvider : ITranslationProvider
{
    private readonly TranslationClient _client;

    public GoogleTranslateProvider(string? credentialsPath = null)
    {
        if (!string.IsNullOrEmpty(credentialsPath))
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
        }

        _client = TranslationClient.Create();
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var response = await _client.TranslateTextAsync(text, targetLanguage, cancellationToken: cancellationToken);
        return response.TranslatedText;
    }
}
