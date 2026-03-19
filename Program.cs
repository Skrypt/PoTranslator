using System.CommandLine;
using PoTranslator;
using PoTranslator.Providers;

var providerOption = new Option<string>("--provider")
{
    Description =
        """
        Translation provider to use:
          google-service-account  Google Translate with service account credentials
          google-api-key          Google Translate with API key
          openai                  OpenAI API (default model: gpt-4.1-nano)
          anthropic               Anthropic Claude API (default model: claude-sonnet-4-5-20250514)
          github-models           GitHub Models / Copilot (default model: gpt-4.1-nano)
          openrouter              OpenRouter (default model: anthropic/claude-sonnet-4-5-20250514)
        """,
    Required = true,
};

var langOption = new Option<string>("--lang")
{
    Description = "Target language code (e.g., fr, it, es, de)",
    Required = true,
};

var poSourceOption = new Option<string>("--po-source")
{
    Description = "Source directory containing .po/.pot files",
    Required = true,
};

var poDestOption = new Option<string>("--po-dest")
{
    Description = "Destination directory for translated .po files",
    Required = true,
};

var apiKeyOption = new Option<string?>("--api-key")
{
    Description = "API key (or set via env: GOOGLE_API_KEY, OPENAI_API_KEY, ANTHROPIC_API_KEY, GITHUB_TOKEN, OPENROUTER_API_KEY)",
};

var projectIdOption = new Option<string?>("--project-id")
{
    Description = "Google Cloud project ID (for google-service-account provider)",
};

var credentialsOption = new Option<string?>("--credentials")
{
    Description = "Path to Google service account JSON file (or set GOOGLE_APPLICATION_CREDENTIALS env var)",
};

var modelOption = new Option<string?>("--model")
{
    Description = "Override the default AI model for the selected provider",
};

var benchmarkSourceOption = new Option<string>("--source")
{
    Description = "Source directory containing .po/.pot files to benchmark",
    Required = true,
};

var benchmarkIterationsOption = new Option<int>("--iterations")
{
    Description = "Number of iterations for the parser benchmark",
    DefaultValueFactory = _ => 100,
};

var benchmarkCommand = new Command("benchmark", "Compare Karambolo vs Parlot PO parser performance")
{
    benchmarkSourceOption,
    benchmarkIterationsOption,
};

benchmarkCommand.SetAction((parseResult, _) =>
{
    var source = parseResult.GetValue(benchmarkSourceOption)!;
    var iterations = parseResult.GetValue(benchmarkIterationsOption);
    ParserBenchmark.Run(source, iterations);
    return Task.CompletedTask;
});

var rootCommand = new RootCommand("PO file translator using Google Translate or AI providers")
{
    providerOption,
    langOption,
    poSourceOption,
    poDestOption,
    apiKeyOption,
    projectIdOption,
    credentialsOption,
    modelOption,
    benchmarkCommand,
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var provider = parseResult.GetValue(providerOption)!;
    var lang = parseResult.GetValue(langOption)!;
    var poSource = parseResult.GetValue(poSourceOption)!;
    var poDest = parseResult.GetValue(poDestOption)!;
    var apiKey = parseResult.GetValue(apiKeyOption);
    var projectId = parseResult.GetValue(projectIdOption);
    var credentials = parseResult.GetValue(credentialsOption);
    var model = parseResult.GetValue(modelOption);

    ITranslationProvider translationProvider;

    try
    {
        translationProvider = CreateProvider(provider, apiKey, projectId, credentials, model);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        return;
    }

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"Provider: {provider}");
    Console.WriteLine($"Language: {lang}");
    Console.WriteLine($"Source:   {poSource}");
    Console.WriteLine($"Dest:     {poDest}");

    if (model is not null)
    {
        Console.WriteLine($"Model:    {model}");
    }

    Console.ResetColor();
    Console.WriteLine();

    try
    {
        var orchestrator = new TranslationOrchestrator(translationProvider, lang);
        await orchestrator.TranslateDirectoryAsync(poSource, poDest, cancellationToken);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Translation completed successfully!");
        Console.ResetColor();
    }
    finally
    {
        if (translationProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
});

return await rootCommand.Parse(args).InvokeAsync();

static ITranslationProvider CreateProvider(string provider, string? apiKey, string? projectId, string? credentials, string? model)
{
    return provider.ToLowerInvariant() switch
    {
        "google-service-account" => CreateGoogleServiceAccount(credentials),
        "google-api-key" => new GoogleApiKeyTranslateProvider(ResolveApiKey(apiKey, "GOOGLE_API_KEY")),
        "openai" => OpenAiCompatibleProvider.CreateOpenAi(
            ResolveApiKey(apiKey, "OPENAI_API_KEY"),
            model ?? "gpt-4.1-nano"),
        "anthropic" => new AnthropicProvider(
            ResolveApiKey(apiKey, "ANTHROPIC_API_KEY"),
            model ?? "claude-sonnet-4-5-20250514"),
        "github-models" => OpenAiCompatibleProvider.CreateGitHubModels(
            ResolveApiKey(apiKey, "GITHUB_TOKEN"),
            model ?? "gpt-4.1-nano"),
        "openrouter" => OpenAiCompatibleProvider.CreateOpenRouter(
            ResolveApiKey(apiKey, "OPENROUTER_API_KEY"),
            model ?? "anthropic/claude-sonnet-4-5-20250514"),
        _ => throw new ArgumentException(
            $"Unknown provider '{provider}'. Valid providers: google-service-account, google-api-key, openai, anthropic, github-models, openrouter"),
    };
}

static GoogleTranslateProvider CreateGoogleServiceAccount(string? credentials)
{
    return new GoogleTranslateProvider(credentials);
}

static string ResolveApiKey(string? apiKey, string envVarName)
{
    return apiKey
        ?? Environment.GetEnvironmentVariable(envVarName)
        ?? throw new ArgumentException(
            $"API key is required. Provide --api-key or set the {envVarName} environment variable.");
}
