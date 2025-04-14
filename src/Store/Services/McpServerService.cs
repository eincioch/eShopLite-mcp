using McpToolsEntities;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using SearchEntities;

namespace Store.Services;

public class McpServerService
{
    private readonly ILogger<ProductService> logger;
    IMcpClient mcpClient = null!;
    IList<McpClientTool> tools = null!;
    private Microsoft.Extensions.AI.IChatClient? chatClient;
    private IList<ChatMessage> ChatMessages = [];

    public McpServerService(ILogger<ProductService> _logger, IMcpClient _mcpClient, IChatClient? _chatClient)
    {
        logger = _logger;
        mcpClient = _mcpClient;
        chatClient = _chatClient;

        // get mcp server tools
        tools = mcpClient.ListToolsAsync().GetAwaiter().GetResult();
    }

    public IList<McpClientTool> GetTools()
    {
        return tools;
    }

    public async Task<SearchResponse?> Search(string searchTerm)
    {
        try
        {
            // init chat messages
            ChatMessages = [];
            ChatMessages.Add(new ChatMessage(ChatRole.System, "You are a helpful assistant. You always replies using text and emojis. You only do what the user ask you to do. If you don't have a function or a tool to answer a question, you just answer que question."));

            ChatOptions chatOptions = new ChatOptions
            {
                Tools = [.. tools],
                ToolMode = ChatToolMode.RequireAny
            };

            // call the desired Endpoint
            ChatMessages.Add(new ChatMessage(ChatRole.User, searchTerm));
            var responseComplete = await chatClient.GetResponseAsync(
                ChatMessages,
                chatOptions);
            logger.LogInformation($"Model Response: {responseComplete}");
            ChatMessages.AddMessages(responseComplete);

            // create search response
            SearchResponse searchResponse = new SearchResponse
            {
                Response = responseComplete.Text
            };


            // iterate through the messages
            foreach (var message in responseComplete.Messages)
            {
                // validate if the message is a function call
                if (message.Role == ChatRole.Tool)
                {
                    var functionResult = message.Contents.FirstOrDefault() as FunctionResultContent;
                    string functionResultJsonString = functionResult.Result.ToString();

                    // from the functionResultJson, get the element at [JSON].content.[0].text
                    // this is the serialization from the function call response object
                    var functionResultJson = System.Text.Json.JsonDocument.Parse(functionResultJsonString);
                    var searchResponseJson = functionResultJson.RootElement.GetProperty("content").EnumerateArray().FirstOrDefault().GetProperty("text").ToString();


                    // try to deserialize the message.RawRepresentation, in Json, to a SearchResponse object
                    try
                    {
                        var deserializedToolResponse = DeserializeResponseJson(searchResponseJson);
                        deserializedToolResponse.ToolCallId = functionResult.CallId;

                        searchResponse.McpFunctionCallId = deserializedToolResponse.ToolCallId;
                        searchResponse.McpFunctionCallName = deserializedToolResponse.ToolName;

                        if (deserializedToolResponse is ProductsSearchToolResponse productsSearchToolResponse)
                        {
                            searchResponse.Products = productsSearchToolResponse.response.Products;
                        }
                        else if (deserializedToolResponse is JokeToolResponse jokeToolResponse)
                        {
                            searchResponse.Response = jokeToolResponse.Joke;
                        }
                        else if (deserializedToolResponse is WeatherToolResponse weatherToolResponse)
                        {
                            searchResponse.Response = weatherToolResponse.WeatherCondition;
                        }
                    }
                    catch (Exception exc)
                    {
                        logger.LogError(exc, "Error deserializing function result JSON to SearchResponse object.");
                    }
                }
            }


            return searchResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Search.");
        }

        return new SearchResponse { Response = "No response" };
    }

    private ToolResponse? DeserializeResponseJson(string json)
    {
        try
        {
            // First try to determine the type of response based on the JSON structure
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
            var rootElement = jsonDoc.RootElement;

            // Check for JokeResponse properties
            if (rootElement.TryGetProperty("Joke", out _) ||
                rootElement.TryGetProperty("Topic", out _))
            {
                var jokeResponse = System.Text.Json.JsonSerializer.Deserialize<JokeToolResponse>(json);
                logger.LogInformation($"Deserialized JSON as JokeResponse: Topic={jokeResponse?.Topic}, Joke available={!string.IsNullOrEmpty(jokeResponse?.Joke)}");
                return jokeResponse;
            }

            // Check for WeatherResponse properties
            if (rootElement.TryGetProperty("CityName", out _) ||
                rootElement.TryGetProperty("WeatherCondition", out _))
            {
                var weatherResponse = System.Text.Json.JsonSerializer.Deserialize<WeatherToolResponse>(json);
                logger.LogInformation($"Deserialized JSON as WeatherResponse: City={weatherResponse?.CityName}, Condition={weatherResponse?.WeatherCondition}");
                return weatherResponse;
            }

            // Check for SearchResponse properties (Products, Response, etc.)
            if (rootElement.TryGetProperty("Products", out _) ||
                rootElement.TryGetProperty("McpFunctionCallName", out _))
            {
                var searchResponse = System.Text.Json.JsonSerializer.Deserialize<ProductsSearchToolResponse>(json);
                logger.LogInformation($"Deserialized JSON as SearchResponse: Products count={searchResponse?.response.Products?.Count ?? 0}");
                return searchResponse;
            }

            // Default to SearchResponse if no specific type was detected
            logger.LogWarning("Could not determine specific response type, defaulting to SearchResponse");
            return System.Text.Json.JsonSerializer.Deserialize<ProductsSearchToolResponse>(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error deserializing JSON");
            return null;
        }
    }

}