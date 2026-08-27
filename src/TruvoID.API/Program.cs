using TruvoID.API.Extensions;
using TruvoID.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddTruvoIDServices(builder.Configuration);

// CORS for dashboard SPA and external API consumers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();

// API key authentication (applies to /v1/* endpoints)
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/v1"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyAuthenticationMiddleware>());

app.MapControllers();

app.Run();
