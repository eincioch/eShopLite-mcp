using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using McpToolsEntities;
using OnlineResearcher.Controllers;

namespace OnlineResearcher.EndPoints;

public static class OnlineResearchEndPoints
{
    public static void MapOnlineResearchEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        routes.MapGet("/", () => $"Online Research API - {DateTime.Now}").ExcludeFromDescription();

        routes.MapGet("/searchonline/{query}",
            async (string query,
            ILogger<Program> logger,
            IConfiguration config) =>
            {
                return await SearchOnlineAsync(query, logger, config);
            })
            .WithName("SearchOnline")
            .Produces<OnlineSearchToolResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    internal static async Task<OnlineSearchToolResponse> SearchOnlineAsync(string query, ILogger<Program> logger, IConfiguration config)
    {
        logger.LogInformation("==========================");
        logger.LogInformation($"Search online for the query: {query}");

        // read settings from user secrets
        var cnnstring = config["aifoundryproject_cnnstring"];
        var tenantid = config["aifoundryproject_tenantid"];
        var searchagentid = config["aifoundryproject_searchagentid"];
        var bingsearchconnectionName = config["aifoundryproject_groundingcnnname"];

        // show in the log the values from config
        logger.LogInformation($"Configuration values:");
        logger.LogInformation($"AI Foundry Project - cnnstring: {cnnstring}");
        logger.LogInformation($"AI Foundry Project - tenantid: {tenantid}");
        logger.LogInformation($"AI Foundry Project - searchagentid: {searchagentid}");
        logger.LogInformation($"AI Foundry Project - bingsearchconnectionName: {bingsearchconnectionName}");

        // Adding the custom headers policy
        var clientOptions = new AIProjectClientOptions();
        clientOptions.AddPolicy(new CustomHeadersPolicy(), HttpPipelinePosition.PerCall);

        // create credential
        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantid))
            options.TenantId = tenantid;
        AIProjectClient projectClient = new AIProjectClient(cnnstring, new DefaultAzureCredential(options), clientOptions);

        AgentsClient agentClient = projectClient.GetAgentsClient();
        Agent? searchOnlineAgent = null;

        if (string.IsNullOrEmpty(searchagentid))
        {
            string connectionId = "";
            var tools = new List<ToolDefinition>();

            if (!string.IsNullOrEmpty(bingsearchconnectionName))
            {
                ConnectionResponse bingConnection = await projectClient.GetConnectionsClient().GetConnectionAsync(bingsearchconnectionName);
                connectionId = bingConnection.Id;
                ToolConnectionList connectionList = new ToolConnectionList
                {
                    ConnectionList = { new ToolConnection(connectionId) }
                };
                BingGroundingToolDefinition bingGroundingTool = new BingGroundingToolDefinition(connectionList);
                tools.Add(bingGroundingTool);
            }

            var agentResponse = await agentClient.CreateAgentAsync(
                model: "gpt-4-1106-preview",
                name: "my-assistant",
                instructions: "You are a helpful assistant that searches online for information.",
                tools: tools);
            searchOnlineAgent = agentResponse.Value;
        }
        else
        {
            searchOnlineAgent = (await agentClient.GetAgentAsync(searchagentid)).Value;
        }

        // Create thread for communication
        var threadResponse = await agentClient.CreateThreadAsync();
        AgentThread thread = threadResponse.Value;

        // Create message to thread
        var messageResponse = await agentClient.CreateMessageAsync(
            thread.Id,
            MessageRole.User,
            $"{query}");
        ThreadMessage message = messageResponse.Value;

        // Run the agent
        var runResponse = await agentClient.CreateRunAsync(thread, searchOnlineAgent);

        while (runResponse.Value.Status == RunStatus.Queued || runResponse.Value.Status == RunStatus.InProgress)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            runResponse = await agentClient.GetRunAsync(thread.Id, runResponse.Value.Id);
        }

        var afterRunMessagesResponse = await agentClient.GetMessagesAsync(thread.Id);
        var messages = afterRunMessagesResponse.Value.Data;

        string searchResult = "";
        logger.LogInformation("==========================");
        logger.LogInformation($"Search for '{query}'");
        foreach (ThreadMessage threadMessage in messages)
        {
            logger.LogInformation($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role,10}: ");
            if (threadMessage.Role.ToString().ToLower() == "assistant")
            {
                foreach (MessageContent contentItem in threadMessage.ContentItems)
                {
                    if (contentItem is MessageTextContent textItem)
                    {
                        searchResult += textItem.Text + "\n";
                    }
                }
            }
        }
        logger.LogInformation($"Search result:");
        logger.LogInformation(searchResult);
        logger.LogInformation("==========================");

        return new OnlineSearchToolResponse()
        {
            SearchTerm = query,
            SearchResults = searchResult
        };
    }
}
