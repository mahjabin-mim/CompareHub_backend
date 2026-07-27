using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CompareHub.Backend.app.Core.Domain.Entities;
using CompareHub.Backend.app.Core.Infrastructure.Auth;
using CompareHub.Backend.app.Core.Infrastructure.Middleware;
using CompareHub.Backend.app.Core.Infrastructure.Persistence;
using CompareHub.Backend.app.Core.Infrastructure.Persistence.Repositories;
using CompareHub.Backend.app.Core.Infrastructure.Services;
using CompareHub.Backend.app.Core.Modules.Auth.Interfaces;
using CompareHub.Backend.app.Core.Modules.Auth.Services;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services.Pipeline;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Services.Scrapers;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Strategies;
using CompareHub.Backend.app.Core.Modules.ProductDiscovery.Utilities;
using CompareHub.Backend.app.Core.Modules.ProductSources.Interfaces;
using CompareHub.Backend.app.Core.Modules.ProductSources.Services;
using CompareHub.Backend.app.Core.Modules.SourceLinks.Interfaces;
using CompareHub.Backend.app.Core.Modules.SourceLinks.Services;
using CompareHub.Backend.app.Core.Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendCorsPolicy";

builder.Configuration.AddJsonFile("app/Host/API/appsettings.json", optional: false, reloadOnChange: true);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISourceLinkService, SourceLinkService>();
builder.Services.AddScoped<IProductSourceService, ProductSourceService>();
builder.Services.AddScoped<IProductDiscoveryService, ProductDiscoveryService>();
builder.Services.AddScoped<IJsonPathExtractor, JsonPathExtractor>();
builder.Services.AddScoped<IApiKeyProtector, ApiKeyProtector>();
builder.Services.AddScoped<IProductExtractionPipeline, ProductExtractionPipeline>();
builder.Services.AddScoped<IProductExtractionStrategy, ApiExtractionStrategy>();
builder.Services.AddScoped<IProductExtractionStrategy, JsonLdExtractionStrategy>();
builder.Services.AddScoped<IProductExtractionStrategy, HtmlSelectorExtractionStrategy>();
builder.Services.AddScoped<IProductExtractionStrategy, PlaywrightExtractionStrategy>();
builder.Services.AddScoped<IProductScraperService, WebsiteProductScraperService>();
builder.Services.AddScoped<IProductSourceScraper, KireiBdProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, KoreanMartProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, ShajgojProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, GroomlyBdProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, SkinCareShopProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, BeautyBoothProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, EmartwaySkincareProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, TekkaProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, DhaliShopProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, TheAlamsProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, TheLiliumProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, KlassyProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, PixieLabellaProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, TheMartBangladeshTumblrProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, SkinnoraProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, MakeupChariProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, PerfectoBdProductScraper>();
builder.Services.AddScoped<IProductSourceScraper, AroggaProductScraper>();
builder.Services.AddHttpClient(ProductSourceScraperBase.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddHttpClient("ProductExtractionClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(35);
});
builder.Services.AddScoped<IProductSourceConnector, ProductSourceConnector>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Avoid blocking EF Core design-time commands (migrations) on app.Run().
var isEfDesignTime = AppDomain.CurrentDomain.GetAssemblies()
    .Any(x => x.GetName().Name?.Equals("Microsoft.EntityFrameworkCore.Design", StringComparison.OrdinalIgnoreCase) == true);
isEfDesignTime = isEfDesignTime || args.Contains("--ef-design-time");

if (!isEfDesignTime)
{
    app.Run();
}
