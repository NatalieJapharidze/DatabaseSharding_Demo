using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Infrastructure.Configuration;
using Infrastructure.Data.Contexts;
using Infrastructure.Health;
using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Configuration
            services.Configure<ShardingOptions>(configuration.GetSection(ShardingOptions.SectionName));

            // Services
            services.AddSingleton<IHashingService, ConsistentHashingService>();
            services.AddScoped<IShardingService, ShardingService>();
            services.AddScoped<IShardConnectionService, ShardConnectionService>();
            services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();
            services.AddScoped<IShardMetricsService, ShardMetricsService>();
            services.AddScoped<IShardHealthService, ShardHealthService>();

            // Repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IShardRepository, ShardRepository>();

            // Background Services
            // Background Services (Register last to ensure dependencies are available)
            services.AddSingleton<ShardRebalancingService>();
            services.AddHostedService(provider => provider.GetRequiredService<ShardRebalancingService>());
            services.AddHostedService<ShardRebalancingService>();

            // Health Check Services
            services.AddScoped<DatabaseHealthCheck>();

            // Health Checks
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database");

            return services;
        }

        public static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ShardDbContext>(options =>
                options.UseNpgsql(connectionString));

            return services;
        }
    }
}
