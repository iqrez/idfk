using Microsoft.Extensions.DependencyInjection;
using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAntiRecoilServices(this IServiceCollection services)
    {
        services.AddSingleton<IRecoilProcessor, RecoilProcessor>();
        services.AddSingleton<IPatternRepository, PatternRepository>();
        services.AddSingleton<IPatternRecorder, PatternRecorder>();
        services.AddSingleton<ISettingsManager<AntiRecoilSettings>>(provider => 
            new SettingsManager<AntiRecoilSettings>("antirecoil_settings.json"));
        // Register ProfileService with a logger and default profiles path
        services.AddSingleton<ProfileService>(provider =>
        {
            var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ProfileService>>();
            return new ProfileService(logger, "Profiles");
        });
        
        return services;
    }
}