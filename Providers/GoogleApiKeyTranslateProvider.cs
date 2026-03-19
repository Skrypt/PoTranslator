using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoTranslator.Providers;

public sealed class GoogleApiKeyTranslateProvider : ITranslationProvider, IDisposable
{
    private const string BaseUrl = "https://translation.googleapis.com/language/translate/v2";

    private readonly HttpClient _httpClient = new();
    private readonly string _apiKey;

    public GoogleApiKeyTranslateProvider(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _apiKey = apiKey;
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            q = text,
            target = targetLanguage,
            format = "text",
        };

        var url = $"{BaseUrl}?key={_apiKey}";
        var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GoogleTranslateResponse>(cancellationToken);

        return result?.Data?.Translations?.FirstOrDefault()?.TranslatedText
            ?? throw new InvalidOperationException("No translation returned from Google Translate API.");
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class GoogleTranslateResponse
    {
        [JsonPropertyName("data")]
        public GoogleTranslateData? Data { get; set; }
    }

    private sealed class GoogleTranslateData
    {
        [JsonPropertyName("translations")]
        public List<GoogleTranslation>? Translations { get; set; }
    }

    private sealed class GoogleTranslation
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }
    }
}
