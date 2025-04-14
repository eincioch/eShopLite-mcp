using McpToolsEntities;

namespace eShopMcpSseServer.Services;

public class OnlineResearcherService
{
    HttpClient httpClient;
    private readonly ILogger<ProductService> _logger;

    public OnlineResearcherService(HttpClient httpClient, ILogger<ProductService> logger)
    {
		_logger = logger;
        this.httpClient = httpClient;
    }

    public async Task<OnlineSearchToolResponse?> Search(string searchTerm)
    {
        try
        {
            // call the desired Endpoint
            HttpResponseMessage response = null;
            response = await httpClient.GetAsync($"/api/research/{searchTerm}");

            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation($"Http status code: {response.StatusCode}");
            _logger.LogInformation($"Http response content: {responseText}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OnlineSearchToolResponse>();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                _logger.LogError($"Internal Server Error: {responseText}");
                throw new Exception($"Internal Server Error: {responseText}");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"Not Found: {responseText}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Search.");
            throw ex;
        }

        return new OnlineSearchToolResponse { SearchResults = "No response" };
    }    
}
