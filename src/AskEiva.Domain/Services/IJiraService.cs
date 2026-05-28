using System;
using System.Threading.Tasks;
using AskEiva.Domain.Services;

namespace AskEiva.Domain.Services;

public interface IJiraService
{
    /// <summary>
    /// Queries a single batch page of target issues out of Jira using custom JQL syntax parameters.
    /// </summary>
    Task<JiraSearchResponse?> GetIssuesPageAsync(string jql, int startAt, int maxResults = 50);
}