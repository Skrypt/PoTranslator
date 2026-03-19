param(
    [string]$Provider = "google-service-account",
    [string]$Lang = "fr",
    [string]$ApiKey,
    [string]$Model
)

$CurrentPath = Get-Location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Get-Item $ScriptDir).Parent.Parent.FullName
$SourcePath = Join-Path $RepoRoot "src"
$TargetPath = Join-Path $RepoRoot "tmp\translations"
$HostPath = Join-Path $SourcePath "OrchardCore.Cms.Web\Localization"

# Clean and create temp directory
if (Test-Path -Path $TargetPath) {
    Remove-Item (Get-Item $TargetPath).Parent.FullName -Recurse -Force
}
New-Item -Path $TargetPath -ItemType Directory -Force | Out-Null

# Install and run PO extractor
dotnet tool uninstall --global OrchardCoreContrib.PoExtractor 2>$null
dotnet tool install --global OrchardCoreContrib.PoExtractor
extractpo $SourcePath $TargetPath

# Build the translator
dotnet build $ScriptDir --configuration Release --nologo -v q

# Set credentials for Google service account provider
if ($Provider -eq "google-service-account") {
    $env:GOOGLE_APPLICATION_CREDENTIALS = Join-Path $ScriptDir "google-credentials.json"
}

# Build arguments
$TranslatorArgs = @(
    "--provider", $Provider,
    "--lang", $Lang,
    "--po-source", $TargetPath,
    "--po-dest", (Join-Path $HostPath $Lang)
)

if ($ApiKey) {
    $TranslatorArgs += @("--api-key", $ApiKey)
}

if ($Model) {
    $TranslatorArgs += @("--model", $Model)
}

# Run the translator
dotnet run --project $ScriptDir --configuration Release --no-build -- @TranslatorArgs

# Clean up
Remove-Item (Get-Item $TargetPath).Parent.FullName -Recurse -Force

Write-Host "Translation completed successfully!" -ForegroundColor Green
