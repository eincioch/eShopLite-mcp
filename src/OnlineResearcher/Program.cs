using OnlineResearcher.EndPoints;
using OpenAI.Chat;
using OpenAI.Embeddings;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IConfiguration>(sp =>
{
    return builder.Configuration;
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOnlineResearchEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
