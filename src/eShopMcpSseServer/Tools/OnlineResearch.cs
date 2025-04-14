using eShopMcpSseServer.Services;
using McpToolsEntities;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;

namespace eShopMcpSseServer.Tools;

[McpServerToolType]
public static class OnlineResearch
{
    [McpServerTool(Name = "OnlineSearch"), Description("Performs a search online using Bing Search APIs. Returns a text with the found content online")]
    public static async Task<OnlineSearchToolResponse> OnlineSearch(
        ILogger<ProductService> logger,
        OnlineResearcherService researcherService,
        [Description("The search query to be used in the online search")] string query)
    {
        var searchResponse = await researcherService.Search(query);
        return searchResponse;
    }
}
