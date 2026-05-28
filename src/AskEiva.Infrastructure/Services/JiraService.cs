using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using AskEiva.Domain.Services; 
using Microsoft.Extensions.Options;

namespace AskEiva.Infrastructure.Services;

public class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly JiraConfiguration _config;

    public JiraService(HttpClient httpClient, IOptions<JiraConfiguration> configOptions)
    {
        _httpClient = httpClient;
        _config = configOptions.Value;

        if (string.IsNullOrEmpty(_config.BaseUrl))
            throw new ArgumentNullException(nameof(_config.BaseUrl), "Jira BaseUrl configuration property is missing inside local user-secrets.");

        // Initialize HttpClient boundaries tailored exactly to Atlassian's Gateway requirements
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Compile and append the secure Basic Auth Header (Email:Token -> Base64 string payload)
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.Email}:{_config.ApiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
    }

    public async Task<JiraSearchResponse?> GetIssuesPageAsync(string jql, int startAt, int maxResults = 50)
    {
        // Escape complex JQL constraints cleanly so URL parsing characters don't fracture the network request
        var escapedJql = Uri.EscapeDataString(jql);
        
        // Target the standard multi-field projection block layout parameters recommended for LLM indexing
        var url = $"search/jql?jql={escapedJql}&startAt={startAt}&maxResults={maxResults}&fields=summary,project,status,issuetype,description,comment";

        try
        {
            var response = await _httpClient.GetAsync(url);

            // Handle Atlassian rate boundaries natively via recursive backoff loops
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(15);
                Console.WriteLine($"[Jira Guard] Rate limit threshold encountered. Backing off engine for {retryAfter.TotalSeconds}s...");
                await Task.Delay(retryAfter);
                return await GetIssuesPageAsync(jql, startAt, maxResults);
            }

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Jira Service API Error] Failed querying block at startAt {startAt}. Status: {response.StatusCode}, Context: {errorBody}");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<JiraSearchResponse>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Jira Service Critical Failure] An exception intercepted your request stream: {ex.Message}");
            return null;
        }
    }
}