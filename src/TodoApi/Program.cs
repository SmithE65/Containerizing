using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TodoApi.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Add DbContext using SQL Server Provider
var todoConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<TodoContext>(options =>
    options.UseSqlServer(todoConnectionString));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

// Create a logger instance
var logger = app.Services.GetService<ILogger<Program>>()
    ?? throw new InvalidOperationException("Failed to resolve logger for Program");

// Log the connection string at the info level
logger.LogInformation("Connection string: {ConnectionString}", todoConnectionString);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    _ = app.UseSwagger();
    _ = app.UseSwaggerUI();
}

_ = app.UseHttpsRedirection();

_ = app.UseAuthorization();

_ = app.MapControllers();

app.Run();
