using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TruvoID.API.Extensions;
using TruvoID.API.Middleware;
using TruvoID.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "TruvoID",
        ValidAudience = jwtSettings["Audience"] ?? "TruvoID",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.FromMinutes(1) // Reduce default 5min skew
    };
});

builder.Services.AddAuthorization();

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

// Add TruvoID services (DB, Redis, business services)
builder.Services.AddTruvoIDServices(builder.Configuration);

var app = builder.Build();

// Seed default data (pricing rates) on first run
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TruvoID.Infrastructure.Data.TruvoIDDbContext>();
    await db.Database.EnsureCreatedAsync();
    await TruvoID.Infrastructure.Data.SeedData.SeedAsync(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseCors();

// API key authentication for /v1 API endpoints
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/v1"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyAuthenticationMiddleware>());

// Routing must come before Authorization
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Map API controllers
app.MapControllers();

// Blazor Razor components for the dashboard
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();