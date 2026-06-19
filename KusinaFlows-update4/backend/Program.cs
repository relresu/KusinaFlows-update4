var builder = WebApplication.CreateBuilder(args);

// 1. ADD SERVICES TO THE CONTAINER (Must be ABOVE builder.Build())
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Register the Authorization Services (THIS FIXES YOUR CRASH)
builder.Services.AddAuthorization();

// Add your CORS policy configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://127.0.0.1:5500", "http://localhost:5500")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Register your custom Neon database service wrapper
builder.Services.AddSingleton<KusinaFlows.Services.DatabaseService>();

// In-memory bearer-token session store backing the auth middleware below
builder.Services.AddSingleton<KusinaFlows.Services.SessionService>();

// ==========================================================
var app = builder.Build(); // The boundary line
// ==========================================================

// 2. CONFIGURE THE HTTP REQUEST PIPELINE (Must be BELOW builder.Build())
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Order matters here: Activate CORS first, then auth gate, then map endpoints
app.UseCors("AllowFrontend");

// ============================================================================
// AUTH MIDDLEWARE
// Every /api/* route requires a valid "Authorization: Bearer <token>" header,
// except login/logout (no session yet) and the DB connectivity probe.
// Tokens are issued by AuthController.Login and tracked in-memory by
// SessionService — see Services/SessionService.cs.
// ============================================================================
var openPaths = new[] { "/api/auth/login", "/api/auth/logout", "/api/test/connect" };

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    bool isOpenPath = context.Request.Method == "OPTIONS"
        || !path.StartsWith("/api/")
        || Array.Exists(openPaths, p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    if (isOpenPath)
    {
        await next();
        return;
    }

    string? token = context.Request.Headers.Authorization.ToString().Replace("Bearer ", "").Trim();
    var sessionService = context.RequestServices.GetRequiredService<KusinaFlows.Services.SessionService>();

    if (string.IsNullOrEmpty(token) || !sessionService.TryGetSession(token, out var session))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "Session expired or not authenticated. Please log in again." });
        return;
    }

    context.Items["CurrentSession"] = session;
    await next();
});

app.UseAuthorization();

app.MapControllers();

app.Run();