using System.ClientModel;
using System.Text;
using OpenAI;
using OpenAI.Chat;

namespace PoTranslator.Providers;

public sealed class OpenAiCompatibleProvider : ITranslationProvider
{
    private static readonly CompositeFormat SystemPromptTemplate = CompositeFormat.Parse(
        "You are a professional translator for software UI strings (OrchardCore CMS). " +
        "Translate the following text to {0}. " +
        "Rules: " +
        "- Return ONLY the translated text, nothing else. " +
        "- Preserve all placeholders like {{0}}, {{1}}, {{{{0}}}}, etc. exactly as they are. " +
        "- Preserve all HTML tags exactly as they are. " +
        "- Use formal register when appropriate. " +
        "- Do not add quotes around the translation.");

    private readonly ChatClient _chatClient;

    public OpenAiCompatibleProvider(string apiKey, string model, Uri endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var options = new OpenAIClientOptions
        {
            Endpoint = endpoint,
        };

        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        _chatClient = client.GetChatClient(model);
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var systemPrompt = string.Format(null, SystemPromptTemplate, targetLanguage);

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(text),
        };

        var completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);

        return completion.Value.Content[0].Text.Trim();
    }

    public static OpenAiCompatibleProvider CreateOpenAi(string apiKey, string model = "gpt-4.1-nano")
        => new(apiKey, model, new Uri("https://api.openai.com/v1"));

    public static OpenAiCompatibleProvider CreateOpenRouter(string apiKey, string model = "anthropic/claude-sonnet-4-5-20250514")
        => new(apiKey, model, new Uri("https://openrouter.ai/api/v1"));

    public static OpenAiCompatibleProvider CreateGitHubModels(string apiKey, string model = "gpt-4.1-nano")
        => new(apiKey, model, new Uri("https://models.inference.ai.github.com"));
}
