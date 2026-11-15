using LocalChatbot.Api.Features.Chat;
using LocalChatbot.Api.Features.Conversations;
using LocalChatbot.Api.Features.OpenAI.ChatCompletions;
using LocalChatbot.Api.Features.OpenAI.Models;
using LocalChatbot.Api.Infrastructure.Ollama;
using LocalChatbot.Api.Infrastructure.Persistence;
using LocalChatbot.Api.Options;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});


builder.Services.AddHttpClient<OllamaClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<
            Microsoft.Extensions.Options.IOptions<OllamaOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services
    .AddOptions<OllamaOptions>()
    .BindConfiguration(OllamaOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(
        options => Uri.TryCreate(
            options.BaseUrl,
            UriKind.Absolute,
            out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps),
        "Ollama BaseUrl must be a valid HTTP or HTTPS URL.")
    .ValidateOnStart();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapModelEndpoints();
app.MapChatCompletionEndpoints();

app.MapConversationEndpoints();

app.MapChatEndpoints();

app.Run();
