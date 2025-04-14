using SearchEntities;
using System.Text.Json.Serialization;

namespace McpToolsEntities;

public class ProductsSearchToolResponse : ToolResponse
{
    public ProductsSearchToolResponse()
    {
        ToolName = "ProductsSearchTool";
    }

    [JsonPropertyName("response")]
    public SearchResponse response { get; set; } = new SearchResponse();
}
