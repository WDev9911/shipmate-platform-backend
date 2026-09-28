using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class ProductDefinitionRepository : IProductDefinitionRepository
{
    private readonly AppDbContext _context;

    public ProductDefinitionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProductDefinition?> GetByWorkspaceIdAsync(Guid workspaceId) =>
        await _context.ProductDefinitions
            .Include(p => p.Features)
                .ThenInclude(f => f.Dependencies)
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId);

    public async Task<bool> HasSuccessfulAnalysisRunAsync(Guid productDefinitionId) =>
        await _context.AiAnalysisRuns.AnyAsync(r => r.ProductDefinitionId == productDefinitionId && r.Succeeded);

    public async Task AddAsync(ProductDefinition productDefinition) =>
        await _context.ProductDefinitions.AddAsync(productDefinition);

    public async Task AddFeatureAsync(Feature feature) =>
        await _context.Features.AddAsync(feature);

    public void RemoveFeature(Feature feature) =>
        _context.Features.Remove(feature);

    public async Task AddDependencyAsync(FeatureDependency dependency) =>
        await _context.FeatureDependencies.AddAsync(dependency);

    public void RemoveDependency(FeatureDependency dependency) =>
        _context.FeatureDependencies.Remove(dependency);

    public async Task AddAnalysisRunAsync(AiAnalysisRun run) =>
        await _context.AiAnalysisRuns.AddAsync(run);

    public async Task AddChangeLogAsync(FeatureChangeLog changeLog) =>
        await _context.FeatureChangeLogs.AddAsync(changeLog);

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
