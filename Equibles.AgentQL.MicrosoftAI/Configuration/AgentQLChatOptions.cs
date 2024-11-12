namespace Equibles.AgentQL.MicrosoftAI.Configuration;

public class AgentQLChatOptions
{
    public AiProvider Provider { get; set; } = AiProvider.OpenAI;
    public string ApiKey { get; set; }
    public string Endpoint { get; set; }
    public string ModelName { get; set; }
    public int MaxOutputTokens { get; set; } = 4096;

    public string SystemPrompt { get; set; } =
        "You are a helpful database assistant. Use the provided tools to query the database and answer user questions. " +
        "Always use GetDatabaseSchema first to understand the database structure before writing queries. " +
        "When you cannot construct a valid query, use ReportFailure to explain why.";

    public string GetEndpoint()
    {
        if (!string.IsNullOrEmpty(Endpoint))
            return Endpoint;

        return Provider switch
        {
            AiProvider.OpenAI => "https://api.openai.com/v1",
            AiProvider.Ollama => "http://localhost:11434",
            AiProvider.Anthropic => "https://api.anthropic.com/v1",
            _ => throw new ArgumentOutOfRangeException(nameof(Provider))
        };
    }
}
