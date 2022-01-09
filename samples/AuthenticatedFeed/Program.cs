using AuthenticatedFeed;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
Startup.ConfigureServices(builder.Services);
var app = builder.Build();
Startup.Configure(app, app.Environment);
await app.RunAsync();
