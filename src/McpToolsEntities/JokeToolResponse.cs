using System.Text.Json.Serialization;

namespace McpToolsEntities;

public class JokeToolResponse : ToolResponse
{
    public JokeToolResponse()
    {
        ToolName = "JokeTool";
    }

    [JsonPropertyName("Topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("Joke")]
    public string Joke { get; set; } = string.Empty;
}
