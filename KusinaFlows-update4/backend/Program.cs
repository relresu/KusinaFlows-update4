using KusinaFlows.Middleware;
using KusinaFlows.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. ADD SERVICES TO THE CONTAINER (Must be ABOVE builder.Build())
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// JWT bearer authentication — defined in the separate middleware project
// (middleware/JwtAuthExtensions.cs). Registers both authentication and
// authorization services in one call.
builder.Services.AddKusinaFlowsAuth(builder.Configuration);

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

// ABSTRACTION: Controllers depend on these interfaces, never on Npgsql
// directly — see Repositories/IInventoryRepository.cs and IStaffRepository.cs.
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IStaffRepository, StaffRepository>();

// ==========================================================
var app = builder.Build(); // The boundary line
// ==========================================================

// 2. CONFIGURE THE HTTP REQUEST PIPELINE (Must be BELOW builder.Build())
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Order matters here: CORS, then authentication, then authorization, then endpoints
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
