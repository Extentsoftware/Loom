using Loom.Application.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Agents.InProc;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process workflow steps that ship with Phase 1
    /// (today: TranscriptNormalizeStep). Each step is registered as
    /// IInProcStep — the WorkflowEngine picks the right one by StepKey.
    /// </summary>
    public static IServiceCollection AddLoomInProcSteps(this IServiceCollection services)
    {
        services.AddSingleton<IInProcStep, TranscriptNormalizeStep>();
        return services;
    }
}
