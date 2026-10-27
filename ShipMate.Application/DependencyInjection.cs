using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;

namespace ShipMate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => { }, typeof(DependencyInjection).Assembly);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IWorkspaceAccessGuard, WorkspaceAccessGuard>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IProductDefinitionService, ProductDefinitionService>();
        services.AddScoped<IProductDefinitionAnalysisService, ProductDefinitionAnalysisService>();
        services.AddScoped<IFeatureReviewService, FeatureReviewService>();
        services.AddScoped<IFeatureComplexityService, FeatureComplexityService>();
        services.AddScoped<ILockService, LockService>();
        services.AddScoped<IFeatureAssignmentService, FeatureAssignmentService>();
        services.AddScoped<IFeatureChallengeService, FeatureChallengeService>();
        services.AddScoped<IBuildService, BuildService>();
        services.AddScoped<IBuildAnalysisService, BuildAnalysisService>();
        services.AddScoped<IBuildApprovalService, BuildApprovalService>();
        services.AddScoped<IDirectPushDetector, DirectPushDetector>();
        services.AddScoped<IDriftAlertService, DriftAlertService>();
        services.AddScoped<IDriftThresholdService, DriftThresholdService>();
        services.AddScoped<INextBestActionService, NextBestActionService>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddSingleton<BuildAnalysisQueue>();
        services.AddSingleton<IBuildAnalysisQueue>(sp => sp.GetRequiredService<BuildAnalysisQueue>());

        return services;
    }
}
