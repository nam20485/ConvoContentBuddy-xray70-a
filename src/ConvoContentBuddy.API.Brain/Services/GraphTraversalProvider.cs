using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConvoContentBuddy.API.Brain.Services;

/// <summary>
/// Service for traversing the problem relationship graph in PostgreSQL.
/// Implements "complexity crawling" to find related problems.
/// </summary>
public class GraphTraversalProvider
{
    private readonly string _connectionString;
    private readonly ILogger<GraphTraversalProvider> _logger;

    public GraphTraversalProvider(IConfiguration configuration, ILogger<GraphTraversalProvider> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("convocontentbuddy") 
            ?? throw new InvalidOperationException("Database connection string not configured");
        
        _logger.LogInformation("GraphTraversalProvider initialized");
    }

    /// <summary>
    /// Gets related problems for a given problem ID using graph traversal.
    /// </summary>
    /// <param name="problemId">The problem ID to find relations for</param>
    /// <param name="maxDepth">Maximum depth of graph traversal</param>
    /// <returns>List of related problems</returns>
    public async Task<IReadOnlyList<RelatedProblem>> GetRelatedProblemsAsync(int problemId, int maxDepth = 2)
    {
        var related = new List<RelatedProblem>();
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Recursive CTE for graph traversal
        var sql = @"
            WITH RECURSIVE problem_graph AS (
                -- Base case: direct relationships
                SELECT 
                    pe.target_id AS id,
                    pe.relationship_type,
                    1 AS depth,
                    p.title,
                    p.title_slug,
                    p.difficulty
                FROM problem_edges pe
                JOIN problems p ON p.id = pe.target_id
                WHERE pe.source_id = @problemId
                
                UNION ALL
                
                -- Recursive case: follow edges
                SELECT 
                    pe.target_id AS id,
                    pe.relationship_type,
                    pg.depth + 1,
                    p.title,
                    p.title_slug,
                    p.difficulty
                FROM problem_edges pe
                JOIN problems p ON p.id = pe.target_id
                JOIN problem_graph pg ON pg.id = pe.source_id
                WHERE pg.depth < @maxDepth
            )
            SELECT DISTINCT ON (id)
                id, title, title_slug, difficulty, relationship_type, depth
            FROM problem_graph
            ORDER BY id, depth
            LIMIT 10";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("problemId", problemId);
        cmd.Parameters.AddWithValue("maxDepth", maxDepth);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            related.Add(new RelatedProblem(
                Id: reader.GetInt32(0),
                Title: reader.GetString(1),
                TitleSlug: reader.GetString(2),
                Difficulty: reader.GetString(3),
                RelationshipType: reader.GetString(4),
                Depth: reader.GetInt32(5)
            ));
        }

        _logger.LogInformation("Found {Count} related problems for {ProblemId}", related.Count, problemId);
        return related;
    }

    /// <summary>
    /// Gets the shortest path between two problems.
    /// </summary>
    public async Task<IReadOnlyList<int>> GetPathAsync(int sourceId, int targetId)
    {
        var path = new List<int>();
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            WITH RECURSIVE paths AS (
                SELECT 
                    source_id, 
                    target_id, 
                    ARRAY[source_id] AS path
                FROM problem_edges
                WHERE source_id = @sourceId
                
                UNION ALL
                
                SELECT 
                    pe.source_id,
                    pe.target_id,
                    p.path || pe.target_id
                FROM problem_edges pe
                JOIN paths p ON p.target_id = pe.source_id
                WHERE NOT pe.target_id = ANY(p.path)
            )
            SELECT path
            FROM paths
            WHERE target_id = @targetId
            LIMIT 1";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("sourceId", sourceId);
        cmd.Parameters.AddWithValue("targetId", targetId);

        var result = await cmd.ExecuteScalarAsync();
        if (result is int[] pathArray)
        {
            path.AddRange(pathArray);
        }

        return path;
    }
}

/// <summary>
/// Represents a related problem in the graph.
/// </summary>
public record RelatedProblem(
    int Id,
    string Title,
    string TitleSlug,
    string Difficulty,
    string RelationshipType,
    int Depth
);
