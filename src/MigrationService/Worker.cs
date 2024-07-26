using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Diagnostics;
using TodoApi.Data;
using TodoApi.Models;
namespace MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;
    private static readonly ActivitySource _activitySource = new("LocalInventory.MigrationService");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity("Migrating database", ActivityKind.Client);
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();

        await EnsureDatabaseCreatedAsync(dbContext, stoppingToken);
        await RunMigrationsAsync(dbContext, stoppingToken);
        await SeedDataAsync(dbContext, stoppingToken);

        _hostApplicationLifetime.StopApplication();
    }

    private static async Task EnsureDatabaseCreatedAsync(TodoContext dbContext, CancellationToken cancellationToken)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async (ct) =>
        {
            if (!await dbCreator.ExistsAsync(ct))
            {
                await dbCreator.CreateAsync(ct);
            }
        }, cancellationToken);
    }

    private static async Task RunMigrationsAsync(TodoContext dbContext, CancellationToken cancellation)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async (ct) =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await dbContext.Database.MigrateAsync(ct);
            await transaction.CommitAsync(ct);
        }, cancellation);
    }

    private static async Task SeedDataAsync(TodoContext dbContext, CancellationToken cancellationToken)
    {
        TodoItem[] items =
        [
            new() { Name = "Item1" },
            new() { Name = "Item2" },
            new() { Name = "Item3" },
        ];

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async (ct) =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await dbContext.TodoItems.AddRangeAsync(items, ct);
            await dbContext.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }
}
