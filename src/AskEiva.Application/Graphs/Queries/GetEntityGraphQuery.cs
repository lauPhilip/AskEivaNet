using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Entities;
using MediatR;

namespace AskEiva.Application.Graphs.Queries;

// 💡 File-scoped definitions must follow the using directives cleanly
public record GetEntityGraphQuery(string SearchFilter = "") : IRequest<GraphModelResult>;

public record GraphModelResult(
    List<GraphNodeDto> Nodes, 
    List<GraphEdgeDto> Edges,
    GraphMetricsDto Metrics
);

public record GraphNodeDto(string Id, string Label, string Color);
public record GraphEdgeDto(string From, string To, string Label);

public record GraphMetricsDto(
    int TotalTriples,
    int TotalNodes,
    double SemanticDensity,
    int ConnectedClusters
);

public class GetEntityGraphQueryHandler : IRequestHandler<GetEntityGraphQuery, GraphModelResult>
{
    private readonly IGraphRepository _graphRepository;

    public GetEntityGraphQueryHandler(IGraphRepository graphRepository)
    {
        _graphRepository = graphRepository;
    }

    public async Task<GraphModelResult> Handle(GetEntityGraphQuery request, CancellationToken cancellationToken)
    {
        var rawTriples = await _graphRepository.GetAllTriplesAsync();
        
        if (!string.IsNullOrWhiteSpace(request.SearchFilter))
        {
            var filter = request.SearchFilter.ToLower();
            rawTriples = rawTriples.Where(t => 
                t.Subject.ToLower().Contains(filter) || 
                t.Predicate.ToLower().Contains(filter) || 
                t.Object.ToLower().Contains(filter));
        }

        var triplesList = rawTriples.ToList();

        var uniqueEntities = triplesList.Select(t => t.Subject)
            .Union(triplesList.Select(t => t.Object))
            .Distinct()
            .ToList();

        var nodes = uniqueEntities.Select(entity => new GraphNodeDto(
            Id: entity,
            Label: entity,
            Color: entity.Contains("Navi") || entity.Contains("VSLAM") ? "#0A2540" : "#0284C7"
        )).ToList();

        var edges = triplesList.Select(t => new GraphEdgeDto(
            From: t.Subject,
            To: t.Object,
            Label: t.Predicate
        )).ToList();

        int totalTriples = triplesList.Count;
        int totalNodes = nodes.Count;
        double density = totalNodes > 0 ? (double)edges.Count / totalNodes : 0.0;
        
        int uniqueEdgesCount = edges.Select(e => e.From).Union(edges.Select(e => e.To)).Distinct().Count();
        int clusters = totalNodes - uniqueEdgesCount > 0 ? (totalNodes - uniqueEdgesCount) + 1 : 1;

        var metrics = new GraphMetricsDto(totalTriples, totalNodes, density, clusters);

        return new GraphModelResult(nodes, edges, metrics);
    }
}