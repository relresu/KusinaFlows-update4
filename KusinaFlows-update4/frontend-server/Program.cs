// Throwaway static file server — serves the existing frontend/ folder as-is
// on http://localhost:5500 (the origin the backend's CORS policy expects),
// since no Node/Python/PHP is available in this environment to do it.
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var frontendPath = @"C:\Users\User\Downloads\KusinaFlows-update4\KusinaFlows-update4\frontend";
var fileProvider = new PhysicalFileProvider(frontendPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider,
    RequestPath = ""
});

app.MapGet("/", () => Results.Redirect("/login/login.html"));

app.Run();
