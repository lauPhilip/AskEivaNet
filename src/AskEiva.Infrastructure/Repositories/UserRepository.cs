using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AskEiva.Domain.Entities;

namespace AskEiva.Infrastructure.Repositories;

public class UserRepository
{
    private readonly HttpClient _httpClient;

    public UserRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var targetEmail = email.Trim().ToLowerInvariant();

            // 💡 GraphQL query: Explicitly requests ONLY the user matching this exact email
            var gqlQuery = new
            {
                query = $$"""
                {
                Get {
                    ApplicationUser(
                    where: {
                        path: ["email"],
                        operator: Equal,
                        valueText: "{{targetEmail}}"
                    }
                    ) {
                    _additional {
                        id
                    }
                    email
                    passwordHash
                    }
                }
                }
                """
            };

            // Post the GraphQL query to the standard Weaviate endpoint
            var response = await _httpClient.PostAsJsonAsync("/v1/graphql", gqlQuery);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<GqlResponse>();
            
            // Dig down into the GraphQL data payload tree
            var record = result?.Data?.Get?.ApplicationUser?.FirstOrDefault();
            if (record == null)
            {
                return null; 
            }

            return new ApplicationUser
            {
                Id = record.Additional?.Id ?? Guid.NewGuid().ToString(),
                Email = record.Email,
                PasswordHash = record.PasswordHash
            };
        }

    public async Task CreateAsync(ApplicationUser user)
    {
        var payload = new
        {
            @class = "ApplicationUser",
            id = user.Id,
            properties = new Dictionary<string, object>
            {
                { "email", user.Email },
                { "passwordHash", user.PasswordHash }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/objects", payload);
        response.EnsureSuccessStatusCode();
    }
}

// --- 💡 PRODUCTION GRAPHQL DESERIALIZATION SCHEMAS ---
public class GqlResponse
{
    [JsonPropertyName("data")]
    public GqlData? Data { get; set; }
}

public class GqlData
{
    [JsonPropertyName("Get")]
    public GqlGet? Get { get; set; }
}

public class GqlGet
{
    [JsonPropertyName("ApplicationUser")]
    public List<GqlUserRecord>? ApplicationUser { get; set; }
}

public class GqlUserRecord
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("_additional")]
    public GqlAdditional? Additional { get; set; }
}

public class GqlAdditional
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}