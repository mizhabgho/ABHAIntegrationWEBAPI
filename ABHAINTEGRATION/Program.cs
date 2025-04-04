using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// ✅ Register services
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<CertificateService>();  
builder.Services.AddScoped<AbhaVerificationService>();
builder.Services.AddScoped<VerifyOtpService>(); // ✅ Register VerifyOtpService
builder.Services.AddControllers();

var app = builder.Build();

// ✅ Middleware pipeline
app.UseRouting();
app.UseAuthorization();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()); // Useful for debugging APIs

app.MapControllers();

app.Run();
