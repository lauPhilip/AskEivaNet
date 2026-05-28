using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Services;

namespace AskEiva.Infrastructure.Services;

public class FreshdeskService : IFreshdeskService
{
    private readonly HttpClient _httpClient;

    public FreshdeskService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

public async Task<IEnumerable<FreshdeskTicketDto>> GetTicketsPageAsync(int page, int perPage = 30)
    {
        // 💡 THE ULTIMATE ALIGNMENT: Strip out custom status arrays and filter properties entirely.
        // Passing just updated_since alongside include=description tells the core endpoint to dump 
        // the complete historical database ledger matching all lifecycle states.
        var historicalAnchor = Uri.EscapeDataString("2010-01-01T00:00:00Z");
        
        var url = $"tickets?page={page}&per_page={perPage}&updated_since={historicalAnchor}&include=description";

        try
        {
            var response = await _httpClient.GetAsync(url);
            
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10);
                Console.WriteLine($"[Freshdesk Guard] Rate limit hit. Backing off for {retryAfter.TotalSeconds}s...");
                await Task.Delay(retryAfter);
                return await GetTicketsPageAsync(page, perPage);
            }

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Freshdesk Service] API failure on page {page}. Status: {response.StatusCode}, Details: {errorBody}");
                return Enumerable.Empty<FreshdeskTicketDto>();
            }

            var tickets = await response.Content.ReadFromJsonAsync<List<FreshdeskTicketDto>>();
            return tickets ?? Enumerable.Empty<FreshdeskTicketDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Freshdesk Service] Exception on page {page}: {ex.Message}");
            return Enumerable.Empty<FreshdeskTicketDto>();
        }
    }

    // 💡 Helper class to map Freshdesk's search endpoint envelope layout
    private class FreshdeskSearchRoot
    {
        public List<FreshdeskTicketDto> Results { get; set; } = new();
    }


    public async Task<IEnumerable<FreshdeskConversationDto>> GetTicketConversationsAsync(long ticketId)
    {
        var url = $"tickets/{ticketId}/conversations";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                await Task.Delay(retryAfter);
                return await GetTicketConversationsAsync(ticketId);
            }

            if (!response.IsSuccessStatusCode) return Enumerable.Empty<FreshdeskConversationDto>();
            return await response.Content.ReadFromJsonAsync<List<FreshdeskConversationDto>>() ?? Enumerable.Empty<FreshdeskConversationDto>();
        }
        catch
        {
            return Enumerable.Empty<FreshdeskConversationDto>();
        }
    }
}