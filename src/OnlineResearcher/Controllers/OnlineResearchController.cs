using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using McpToolsEntities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace OnlineResearcher.Controllers;

[ApiController]
[Route("[controller]")]
public class OnlineResearchController : ControllerBase
{
    private readonly ILogger<OnlineResearchController> logger;
    private readonly IConfiguration config;

    public OnlineResearchController(ILogger<OnlineResearchController> logger, IConfiguration config)
    {
        this.logger = logger;
        this.config = config;
    }

    [HttpGet(Name = "SearchOnline")]
    public async Task<OnlineSearchToolResponse> SearchOnlineAsync(string query)
    {
        logger.LogInformation("==========================");
        logger.LogInformation($"Search online for the query: {query}");

        // read settings from user secrets
        var cnnstring = config["aifoundryproject:cnnstring"];
        var tenantid = config["aifoundryproject:tenantid"];
        var searchagentid = config["aifoundryproject:searchagentid"];
        var bingsearchconnectionName = config["aifoundryproject:groundingcnnname"];

        // Adding the custom headers policy
        var clientOptions = new AIProjectClientOptions();
        clientOptions.AddPolicy(new CustomHeadersPolicy(), HttpPipelinePosition.PerCall);

        // create credential
        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantid))
            options.TenantId = tenantid;
        AIProjectClient projectClient = new AIProjectClient(cnnstring, new DefaultAzureCredential(options), clientOptions);


        AgentsClient agentClient = projectClient.GetAgentsClient();
        Agent searchOnlineAgent = null;

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

            var agentResponse = agentClient.CreateAgent(
                model: "gpt-4-1106-preview",
                name: "my-assistant",
                instructions: "You are a helpful assistant that search online for information.",
                tools: tools);
            searchOnlineAgent = agentResponse.Value;
        }
        else
        {
            searchOnlineAgent = agentClient.GetAgent(searchagentid).Value;
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

        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            runResponse = await agentClient.GetRunAsync(thread.Id, runResponse.Value.Id);
        }
        while (runResponse.Value.Status == RunStatus.Queued
            || runResponse.Value.Status == RunStatus.InProgress);

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
                foreach (Azure.AI.Projects.MessageContent contentItem in threadMessage.ContentItems)
                {
                    if (contentItem is MessageTextContent textItem)
                    {
                        searchResult += textItem.Text;
                        searchResult += "\n";
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
