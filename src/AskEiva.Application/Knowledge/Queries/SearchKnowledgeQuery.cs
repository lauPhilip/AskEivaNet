using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;
using MediatR;

namespace AskEiva.Application.Knowledge.Queries;

public record SearchKnowledgeQuery(string UserQuestion, int MaxResults) : IRequest<SearchQueryResult>;

public record SearchQueryResult(
    List<RetrievalMatch> SemanticMatches,
    List<KnowledgeTriple> RelevantGraphRelations,
    string SearchQuery,
    IAsyncEnumerable<string> AnswerStream
);

public class SearchKnowledgeQueryHandler : IRequestHandler<SearchKnowledgeQuery, SearchQueryResult>
{
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;
    private readonly IMistralChatService _chatService;

    public SearchKnowledgeQueryHandler(
        IKnowledgeRetrievalRepository retrievalRepository, 
        IMistralChatService chatService)
    {
        _retrievalRepository = retrievalRepository;
        _chatService = chatService;
    }

    public async Task<SearchQueryResult> Handle(SearchKnowledgeQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserQuestion))
        {
            return new SearchQueryResult(new(), new(), string.Empty, EmptyStream());
        }

        // 1. Concurrent sweep across your Weaviate storage nodes
        var semanticTask = _retrievalRepository.SearchSemanticChunksAsync(request.UserQuestion, request.MaxResults);
        var graphTask = _retrievalRepository.SearchGraphTriplesAsync(request.UserQuestion, request.MaxResults);

        await Task.WhenAll(semanticTask, graphTask);

        var semanticMatches = semanticTask.Result.ToList();
        var graphTriples = graphTask.Result.ToList();

        // 2. Pass back the live asynchronous stream handler instead of a blocking string
        var stream = _chatService.GenerateStreamingChatResponseAsync(
            request.UserQuestion, 
            semanticMatches, 
            graphTriples,
            System.Linq.Enumerable.Empty<ChatTurn>()
        );

        return new SearchQueryResult(
            SemanticMatches: semanticMatches,
            RelevantGraphRelations: graphTriples,
            SearchQuery: request.UserQuestion,
            AnswerStream: stream
        );
    }

    private async IAsyncEnumerable<string> EmptyStream()
    {
        yield return "Query cannot be blank.";
        await Task.CompletedTask;
    }
}