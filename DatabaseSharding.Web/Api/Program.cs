using System.Text.Json.Serialization;
using Application;
using Domain;
using Infrastructure;
using Infrastructure.Configuration;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            //Layers DI
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);

            var app = builder.Build();

            // Initialize databases on startup (if configured to do so)
            var shardingOptions = builder.Configuration.GetSection(ShardingOptions.SectionName).Get<ShardingOptions>();
            if (shardingOptions?.InitializeDatabasesOnStartup == true)
            {
                app.Logger.LogInformation("Starting database initialization...");

                using (var scope = app.Services.CreateScope())
                {
                    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();

                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(shardingOptions.DatabaseInitializationTimeoutSeconds));
                        await dbInitializer.InitializeAllDatabasesAsync(cts.Token);
                        app.Logger.LogInformation("Database initialization completed successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        app.Logger.LogError("Database initialization timed out after {Timeout} seconds",
                            shardingOptions.DatabaseInitializationTimeoutSeconds);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        app.Logger.LogError(ex, "Database initialization failed. Application startup aborted.");
                        throw;
                    }
                }
            }
            else
            {
                app.Logger.LogInformation("Database initialization skipped (disabled in configuration)");
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
           
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHealthChecks("/health");

            await app.RunAsync();


            app.Run();
        }
    }
}
