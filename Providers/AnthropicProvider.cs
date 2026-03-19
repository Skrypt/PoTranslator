using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PoTranslator.Providers;

public sealed class AnthropicProvider : ITranslationProvider, IDisposable
{
    private const string BaseUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    private readonly HttpClient _httpClient;
    private readonly string _model;

    public AnthropicProvider(string apiKey, string model = "claude-sonnet-4-5-20250514")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _model = model;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var systemPrompt =
            "You are a professional translator for software UI strings (OrchardCore CMS). " +
            $"Translate the following text to {targetLanguage}. " +
            "Rules: " +
            "- Return ONLY the translated text, nothing else. " +
            "- Preserve all placeholders like {0}, {1}, {{0}}, etc. " +
            "- Preserve all HTML tags exactly as they are. " +
            "- Use formal register when appropriate. " +
            "- Do not add quotes around the translation.";

        var requestBody = new
        {
            model = _model,
            max_tokens = 1024,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = text },
            },
        };

        var response = await _httpClient.PostAsJsonAsync(BaseUrl, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken);

        return result?.Content?.FirstOrDefault()?.Text?.Trim()
            ?? throw new InvalidOperationException("No translation returned from Anthropic API.");
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContent>? Content { get; set; }
    }

    private sealed class AnthropicContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
