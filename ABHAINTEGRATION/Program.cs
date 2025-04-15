using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using AbhaProfileApi.Services; // Added for IProfileManagementService and ProfileManagementService

var builder = WebApplication.CreateBuilder(args);

// ✅ Register services
builder.Services.AddScoped<VerifyUserService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<CertificateService>();
builder.Services.AddScoped<AbhaVerificationService>();
builder.Services.AddScoped<VerifyOtpService>();
builder.Services.AddScoped<IProfileManagementService, ProfileManagementService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddControllers();

var app = builder.Build();

// ✅ Middleware pipeline
app.UseRouting();
app.UseAuthorization();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()); // Useful for debugging APIs

app.MapControllers();

app.Run();