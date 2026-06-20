using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using KusinaFlows.Repositories;
using KusinaFlows.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. ADD SERVICES TO THE CONTAINER (Must be ABOVE builder.Build())
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// JWT bearer authentication. JwtTokenService (Services/JwtTokenService.cs)
// issues tokens on login; this scheme validates them on every subsequent
// request to a [Authorize]-protected endpoint (InventoryController,
// StaffController), before the request ever reaches controller code.
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

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
