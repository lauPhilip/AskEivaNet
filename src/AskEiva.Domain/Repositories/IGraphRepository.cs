using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Repositories;

public interface IGraphRepository
{
    Task SaveTriplesAsync(IEnumerable<KnowledgeTriple> triples);
    Task<IEnumerable<KnowledgeTriple>> GetAllTriplesAsync();
}