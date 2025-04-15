using eShopMcpSseServer.Services;
using McpToolsEntities;
using ModelContextProtocol.Server;
using OpenAI.Chat;
using SearchEntities;
using System.ComponentModel;

namespace eShopMcpSseServer.Tools;

[McpServerToolType]
public static class OnlineResearch
{
    //[McpServerTool(Name = "OnlineSearch"), Description("Performs a search online using Bing Search APIs. Returns a text with the found content online")]
    //public static async Task<OnlineSearchToolResponse> OnlineSearch(
    //    ILogger<ProductService> logger,
    //    OnlineResearcherService researcherService,
    //    [Description("The search query to be used in the online search")] string query)
    //{
    //    var researchResponse = await researcherService.Search(query);
    //    return researchResponse;
    //}

    //[McpServerTool(Name = "OnlineSearchWithOutdoorProducts"), Description("Performs a search online using Bing Search APIs, then search the outdoor product to see if there is a match of products. Returns the online search with a collection of outdoor products.")]
    //public static async Task<ProductsSearchToolResponse> OnlineSearchWithOutdoorProducts(
    [McpServerTool(Name = "OnlineSearch"), Description("Performs a search online using Bing Search APIs. Returns a text with the found content online")]
    public static async Task<ProductsSearchToolResponse> OnlineSearch(
     ILogger<ProductService> logger,
     OnlineResearcherService researcherService,
     ChatClient chatClient,
     ProductService productService,
     [Description("The search query to be used in the online search")] string query)
    {
        // 1. Perform an online search using the Bing Search APIs
        var researchResponse = await researcherService.Search(query);

        // 2. Create a search query from the research response to search for products
        var prompt = @$"Analyze the following response from an online search and generate a query to be used on a semantic search with a vector database for outdoor products.
Return only the query without any other information.
---
Online Research Result: 
{researchResponse.SearchResults}";

        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new UserChatMessage(prompt)
        };
        var resultPrompt = await chatClient.CompleteChatAsync(messages);
        var queryFromChatClient = resultPrompt.Value.Content[0].Text!;


        // 3. Search the products vector database using the query generated from the online search
        SearchResponse response = new();
        try
        {
            response = await productService.Search(queryFromChatClient, true);
            response.McpFunctionCallName = "OnlineSearchWithOutdoorProducts";
            response.Response = researchResponse.SearchResults;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error during Search: {ex.Message}");
            response.Response = $"No response. {ex}";
        }

        // 4. Return the response
        return new ProductsSearchToolResponse()
        {
            response = response
        };
    }
}
