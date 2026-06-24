using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

// Register the Hosted Service background cleaner
builder.Services.AddHostedService<StaleDocumentCleaner>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseCors();

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.MapHub<CodeSharingHub>("/hubs/share");

app.Run();

// Data structure to track state and expiration criteria
public record DocumentMetadata(string Language, string Code, DateTime LastUpdatedAt);

public class CodeSharingHub : Hub
{
    // Global thread-safe state tracking storage
    public static readonly ConcurrentDictionary<string, DocumentMetadata> Docs = new();

    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

        if (Docs.TryGetValue(sessionId, out var doc))
        {
            await Clients.Caller.SendAsync("SyncDocument", doc.Language, doc.Code);
        }
    }

    public async Task UpdateCode(string sessionId, string language, string code)
    {
        var newDoc = new DocumentMetadata(language, code, DateTime.UtcNow);

        // This assignment is completely atomic. 
        // It replaces the old reference with the new one in a single thread-safe step.
        Docs[sessionId] = newDoc;

        // Broadcast the update payload safely
        await Clients.OthersInGroup(sessionId).SendAsync("CodeUpdated", language, code);
    }

    public async Task DeleteDocument(string sessionId)
    {
        if (Docs.TryRemove(sessionId, out _))
        {
            await Clients.Group(sessionId).SendAsync("DocumentDeleted");
        }
    }
}

// Background worker running on a periodic timer
public class StaleDocumentCleaner : BackgroundService
{
    private readonly IHubContext<CodeSharingHub> _hubContext;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(15); // How often the worker checks
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(2);         // Max expiration life

    public StaleDocumentCleaner(IHubContext<CodeSharingHub> hubContext)
    {
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_cleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            foreach (var kvp in CodeSharingHub.Docs)
            {
                if (now - kvp.Value.LastUpdatedAt > _maxAge)
                {
                    // Pass the exact KeyValuePair. 
                    // TryRemove will ONLY succeed if the value in the dictionary 
                    // still perfectly matches the exact reference snapshot we checked.
                    if (CodeSharingHub.Docs.TryRemove(kvp))
                    {
                        await _hubContext.Clients.Group(kvp.Key).SendAsync("DocumentDeleted");
                    }
                }
            }
        }
    }
}