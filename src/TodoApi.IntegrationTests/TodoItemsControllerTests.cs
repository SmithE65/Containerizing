using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Text;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.IntegrationTests;

public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _ = builder.ConfigureAppConfiguration((context, conf) =>
        {
            // Generate a unique database name
            string dbName = Guid.NewGuid().ToString();

            // Read the existing connection string
            IConfigurationRoot configuration = conf.Build();
            // Replace the database name in the connection string
            SqlConnectionStringBuilder connectionString = new(
                configuration.GetConnectionString("DefaultConnection"))
            {
                InitialCatalog = dbName
            };

            // Replace the 'DefaultConnection' configuration value with the new connection string
            _ = conf.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString.ToString()
            });
        });
    }
}

public class TodoItemsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TodoItemsControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Now that the DbContext is registered with the new connection string, we can run migrations.
        using IServiceScope scope = _factory.Services.CreateScope();
        TodoContext dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();
        dbContext.Database.Migrate();
    }

    [Fact]
    public async Task GetTodoItems_ReturnsSuccessStatusCode()
    {
        // Arrange
        HttpRequestMessage request = new(HttpMethod.Get, "/api/TodoItems");

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        _ = response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PutTodoItem_UpdatesTodoItem()
    {
        // Arrange
        TodoItem newTodoItem = new()
        { Name = "Test item", IsComplete = false };
        HttpResponseMessage postResponse = await _client.PostAsJsonAsync(
            "/api/TodoItems",
            newTodoItem);
        TodoItem? todoItem = await postResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(todoItem);

        TodoItem updatedTodoItem = new()
        {
            Id = todoItem.Id,
            Name = "Updated test item",
            IsComplete = true
        };

        HttpRequestMessage putRequest = new(HttpMethod.Put, $"/api/TodoItems/{updatedTodoItem.Id}")
        {
            Content = new StringContent(JsonConvert.SerializeObject(updatedTodoItem), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage putResponse = await _client.SendAsync(putRequest);

        // Assert
        _ = putResponse.EnsureSuccessStatusCode();
        HttpRequestMessage getRequest = new(
            HttpMethod.Get,
            $"/api/TodoItems/{updatedTodoItem.Id}");
        HttpResponseMessage getResponse = await _client.SendAsync(getRequest);
        _ = getResponse.EnsureSuccessStatusCode();

        TodoItem? returnedTodoItem = await getResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(returnedTodoItem);
        Assert.Equal(updatedTodoItem.Name, returnedTodoItem.Name);
        Assert.Equal(updatedTodoItem.IsComplete, returnedTodoItem.IsComplete);
    }
}
