using FoundationalCSharp.Server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
// Configure SignalR with more aggressive keepalive / timeout to detect disconnects faster
builder.Services.AddSignalR(options =>
{
    // server will send ping every 10s
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    // if client does not respond within 30s consider it disconnected
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Allow CORS for the mobile app (adjust origins in production)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true); // allow all origins, change for production
    });
});

var app = builder.Build();

app.UseCors();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/gamehub");

app.Run();
