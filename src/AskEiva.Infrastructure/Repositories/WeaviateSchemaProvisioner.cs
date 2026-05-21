using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AskEiva.Infrastructure.Repositories
{
    public class WeaviateSchemaProvisioner
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeaviateSchemaProvisioner> _logger;

        public WeaviateSchemaProvisioner(HttpClient httpClient, ILogger<WeaviateSchemaProvisioner> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task EnsureSchemaAsync()
        {
            try
            {
                _logger.LogInformation("Checking Weaviate for existing Identity schemas...");

                // 1. Check if the ApplicationUser collection already exists
                var checkResponse = await _httpClient.GetAsync("/v1/schema/ApplicationUser");
                
                if (checkResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Schema 'ApplicationUser' already provisioned in Weaviate.");
                    return;
                }

                _logger.LogWarning("Schema 'ApplicationUser' not found. Initializing provisioning pipeline...");

                // 2. Define the strict collection schema definition matching your Domain Entities
                var schemaDefinition = new
                {
                    @class = "ApplicationUser",
                    description = "Stores encrypted core identity accounts for the AskEiva system mapping matrix.",
                    vectorizer = "none",
                    // 💡 FIX: Using 'new object[]' lets us mix different anonymous shapes in the same collection
                    properties = new object[]
                    {
                        new { name = "email", dataType = new[] { "text" }, description = "The unique operational email identifier.", tokenization = "field" },
                        new { name = "passwordHash", dataType = new[] { "text" }, description = "The cryptographically secure hashed password string." }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(schemaDefinition);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // 3. Post the fresh schema registration mapping to Weaviate
                var provisionResponse = await _httpClient.PostAsync("/v1/schema", content);

                if (provisionResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully provisioned 'ApplicationUser' collection structure into Weaviate.");
                }
                else
                {
                    var errorDetails = await provisionResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Critical Schema Provisioning Failure: {provisionResponse.StatusCode} - {errorDetails}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An unhandled exception collapsed the schema configuration loop.");
            }
        }
    }
}