using System.Threading.Tasks;
using System.Collections.Generic;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

public interface IMistralChatService
{
    // progressive chunk streaming
    IAsyncEnumerable<string> GenerateStreamingChatResponseAsync(
        string userQuestion, 
        IEnumerable<RetrievalMatch> semanticContext, 
        IEnumerable<KnowledgeTriple> structuralGraph,
        IEnumerable<ChatTurn> conversationHistory
    );
}