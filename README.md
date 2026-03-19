# Orchard Core PoTranslator

A .NET 10 command-line tool for translating PO/POT files using Google Translate or AI providers.

## Quick Start

```bash
dotnet run --project .scripts/PoTranslator.Console -- \
  --provider <provider> \
  --lang <language-code> \
  --po-source <source-directory> \
  --po-dest <destination-directory>
```

## Options

| Option | Required | Description |
|---|---|---|
| `--provider` | Yes | Translation provider (see below) |
| `--lang` | Yes | Target language code (e.g., `fr`, `it`, `es`, `de`) |
| `--po-source` | Yes | Directory containing source `.po` or `.pot` files |
| `--po-dest` | Yes | Directory where translated `.po` files will be saved |
| `--api-key` | No | API key (can also be set via environment variables) |
| `--model` | No | Override the default AI model |
| `--credentials` | No | Path to Google service account JSON file |
| `--project-id` | No | Google Cloud project ID |

## Providers

### Google Translate (Service Account)

Uses the Google Cloud Translation API v2 with service account credentials.

**Setup:**

1. Create a project in [Google Cloud Console](https://console.cloud.google.com/)
2. Enable the Cloud Translation API
3. Create a Service Account and download the JSON key file
4. See the [quickstart guide](https://cloud.google.com/translate/docs/quickstart-client-libraries) for details

**Usage:**

```bash
# Option 1: Pass credentials path
dotnet run -- --provider google-service-account --lang fr \
  --credentials ./google-credentials.json \
  --po-source ./source --po-dest ./output

# Option 2: Use environment variable
export GOOGLE_APPLICATION_CREDENTIALS="./google-credentials.json"
dotnet run -- --provider google-service-account --lang fr \
  --po-source ./source --po-dest ./output
```

### Google Translate (API Key)

Uses the Google Cloud Translation API v2 with a simple API key. Easier to set up than a service account.

**Setup:**

1. Create a project in [Google Cloud Console](https://console.cloud.google.com/)
2. Enable the Cloud Translation API
3. Create an API key under **APIs & Services > Credentials**

**Usage:**

```bash
# Option 1: Pass API key directly
dotnet run -- --provider google-api-key --lang fr \
  --api-key YOUR_GOOGLE_API_KEY \
  --po-source ./source --po-dest ./output

# Option 2: Use environment variable
export GOOGLE_API_KEY="YOUR_GOOGLE_API_KEY"
dotnet run -- --provider google-api-key --lang fr \
  --po-source ./source --po-dest ./output
```

### OpenAI

Uses the OpenAI API directly. Default model: `gpt-4.1-nano`.

**Setup:**

1. Create an account at [platform.openai.com](https://platform.openai.com/)
2. Generate an API key under **API Keys**

**Usage:**

```bash
# Option 1: Pass API key directly
dotnet run -- --provider openai --lang fr \
  --api-key YOUR_OPENAI_API_KEY \
  --po-source ./source --po-dest ./output

# Option 2: Use environment variable
export OPENAI_API_KEY="YOUR_OPENAI_API_KEY"
dotnet run -- --provider openai --lang fr \
  --po-source ./source --po-dest ./output

# Option 3: Use a different model
dotnet run -- --provider openai --lang fr \
  --model gpt-4.1 \
  --po-source ./source --po-dest ./output
```

### Anthropic Claude

Uses the Anthropic Messages API directly. Default model: `claude-sonnet-4-5-20250514`.

**Setup:**

1. Create an account at [console.anthropic.com](https://console.anthropic.com/)
2. Generate an API key under **API Keys**

**Usage:**

```bash
# Option 1: Pass API key directly
dotnet run -- --provider anthropic --lang fr \
  --api-key YOUR_ANTHROPIC_API_KEY \
  --po-source ./source --po-dest ./output

# Option 2: Use environment variable
export ANTHROPIC_API_KEY="YOUR_ANTHROPIC_API_KEY"
dotnet run -- --provider anthropic --lang fr \
  --po-source ./source --po-dest ./output

# Option 3: Use a different model
dotnet run -- --provider anthropic --lang fr \
  --model claude-haiku-4-5-20241022 \
  --po-source ./source --po-dest ./output
```

### GitHub Models (Copilot)

Uses GitHub Models, available to GitHub Copilot subscribers. Supports models from OpenAI, Anthropic, Google, and more. Default model: `gpt-4.1-nano`.

**Setup:**

1. You need a GitHub account with an active Copilot subscription
2. Create a personal access token at [github.com/settings/tokens](https://github.com/settings/tokens)

**Usage:**

```bash
# Option 1: Pass token directly
dotnet run -- --provider github-models --lang fr \
  --api-key YOUR_GITHUB_TOKEN \
  --po-source ./source --po-dest ./output

# Option 2: Use environment variable
export GITHUB_TOKEN="YOUR_GITHUB_TOKEN"
dotnet run -- --provider github-models --lang fr \
  --po-source ./source --po-dest ./output

# Option 3: Use a different model (e.g., Claude via GitHub Models)
dotnet run -- --provider github-models --lang fr \
  --model claude-sonnet-4-5-20250514 \
  --po-source ./source --po-dest ./output
```

### OpenRouter

Uses OpenRouter as a unified gateway to 290+ models including Claude, GPT, Gemini, Grok, and more. Default model: `anthropic/claude-sonnet-4-5-20250514`.

**Setup:**

1. Create an account at [openrouter.ai](https://openrouter.ai/)
2. Generate an API key at [openrouter.ai/settings/keys](https://openrouter.ai/settings/keys)

**Usage:**

```bash
# Option 1: Pass API key directly
dotnet run -- --provider openrouter --lang fr \
  --api-key YOUR_OPENROUTER_API_KEY \
  --po-source ./source --po-dest ./output

# Option 2: Use environment variable
export OPENROUTER_API_KEY="YOUR_OPENROUTER_API_KEY"
dotnet run -- --provider openrouter --lang fr \
  --po-source ./source --po-dest ./output

# Option 3: Use a specific model (see https://openrouter.ai/models)
dotnet run -- --provider openrouter --lang fr \
  --model google/gemini-2.5-flash \
  --po-source ./source --po-dest ./output
```

## Helper Scripts

Automated scripts that extract PO files from the OrchardCore source tree and translate them in one step.

### PowerShell

```powershell
# Translate to French using Google service account (default)
./translate.ps1

# Translate to Italian using Anthropic Claude
./translate.ps1 -Provider anthropic -Lang it -ApiKey YOUR_KEY

# Translate using a specific model
./translate.ps1 -Provider openai -Lang es -ApiKey YOUR_KEY -Model gpt-4.1
```

### Bash

```bash
# Translate to French using Google service account (default)
./translate.sh

# Translate to Italian using Anthropic Claude
./translate.sh anthropic it YOUR_KEY

# Translate using a specific model
./translate.sh openai es YOUR_KEY gpt-4.1
```

## Behavior

- **Preserves existing translations**: If the output file already exists, previously translated strings are kept and only missing translations are added.
- **AI system prompt**: AI providers receive context that these are OrchardCore CMS UI strings. They are instructed to preserve placeholders (`{0}`, `{1}`, `{{0}}`), HTML tags, and use formal register.
- **Colored output**: Progress is displayed with color-coded console output (cyan for files, green for translations, gray for skipped, red for errors).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- For PO extraction: `dotnet tool install --global OrchardCoreContrib.PoExtractor`
- A valid API key or credentials for your chosen provider
