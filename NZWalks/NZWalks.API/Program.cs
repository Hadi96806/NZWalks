using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NZWalks.API;
using NZWalks.API.Constants;
using NZWalks.API.Data;
using NZWalks.API.Mappings;
using NZWalks.API.Middlewares;
using NZWalks.API.Repositories;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;



var builder = WebApplication.CreateBuilder(args);

// Secrets live in user-secrets, not appsettings.json (which holds empty placeholders), so a
// missing value is an empty string, not null. Fail fast and say how to fix it.
string RequireSecret(string key)
{
    var value = builder.Configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Missing configuration value '{key}'. Set it with: " +
            $"dotnet user-secrets set \"{key}\" \"<value>\" --project NZWalks.API");
    }
    return value;
}

RequireSecret("ConnectionStrings:NZWalksConnectionString");
RequireSecret("ConnectionStrings:NZWalksAuthConnectionString");

// Token lifetime is not a secret, but a missing value binds to 0 and would mint tokens that are
// already expired, so fail at startup like the secrets above.
if (builder.Configuration.GetValue<int>("Jwt:ExpiryMinutes") <= 0)
{
    throw new InvalidOperationException(
        "Missing or invalid configuration value 'Jwt:ExpiryMinutes'. It must be a positive number of minutes.");
}

// Add services to the container.
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/NZWalksLog.txt", rollingInterval: RollingInterval.Day)
    .MinimumLevel.Information()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);


//FluentValidation owns "required": without this, a non-nullable string that is missing also gets
//MVC's implicit "The X field is required." on top of the validator's message
builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddProblemDetails();

//Every AbstractValidator<T> in this assembly is registered; auto-validation feeds failures into ModelState,
//so [ApiController] returns its usual 400 ValidationProblemDetails. Rules must stay synchronous.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();
//Stop at a field's first failing rule: an empty Name reports "must not be empty", not that plus a length error
ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});


builder.Services.AddHttpContextAccessor();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = JwtBearerDefaults.AuthenticationScheme
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                },
                Scheme = "OAuth2",
                Name = JwtBearerDefaults.AuthenticationScheme,
                In = ParameterLocation.Header
            },
            new List<String>()
        }
    });
});
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

builder.Services.AddDbContext<NZWalksDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("NZWalksConnectionString")));

builder.Services.AddDbContext<NZWalksAuthDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("NZWalksAuthConnectionString")));

builder.Services.AddScoped<IRegionRepository, SQLRegionRepository>();
builder.Services.AddScoped<IWalkRepository, SQLWalkRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IImageRepository, LocalImageRepository>();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<AutoMapperProfiles>();
});

builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddTokenProvider<DataProtectorTokenProvider<IdentityUser>>("NZWalks")
    .AddEntityFrameworkStores<NZWalksAuthDbContext>()
    //Only for CheckPasswordSignInAsync (lockout-aware password check); no cookie sign-in is used
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
    {
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 1;

        //Lock an account for 5 minutes after 5 wrong passwords in a row - Identity's defaults, set explicitly so they're visible.
        //Bounds guessing against one account from many IPs, which the per-IP rate limit can't.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    });

var jwtKey = RequireSecret("Jwt:Key");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)),
        //Tokens are only ever signed with HS256 (TokenRepository), so reject any other alg
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        //One process issues and validates on one clock, so the default 5-minute grace past exp is not needed
        ClockSkew = TimeSpan.Zero
    });

//Named policies replace the role strings repeated on the controllers; RequireRole(a, b, c) means "any of them"
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.CanRead, policy => policy.RequireRole(RoleNames.Reader, RoleNames.Writer, RoleNames.Admin));
    options.AddPolicy(PolicyNames.CanWrite, policy => policy.RequireRole(RoleNames.Writer, RoleNames.Admin));
    options.AddPolicy(PolicyNames.CanAdmin, policy => policy.RequireRole(RoleNames.Admin));

    //Fail closed: an endpoint with no authorization metadata at all needs a logged-in user, so a new
    //controller is protected by default. [AllowAnonymous] or any [Authorize] on an action overrides this.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});




//Global cap: 100 requests per minute per client IP, fixed window, no queue (extra requests are rejected, not delayed).
//Limiter state is in memory, so it resets on restart and is per instance.
builder.Services.AddRateLimiter(options =>
{
    //429 says "you are too fast"; the default 503 would tell clients the server is broken
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        string? detail = null;

        //Repeated 429s from one IP on /api/Auth are a brute-force signal worth seeing in the logs
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("NZWalks.RateLimiting");
        logger.LogWarning("Rate limit hit by {ClientIp} on {Method} {Path}",
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", httpContext.Request.Method, httpContext.Request.Path);

        //Tell well-behaved clients exactly when to retry instead of letting them hammer
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            httpContext.Response.Headers.RetryAfter = seconds.ToString(NumberFormatInfo.InvariantInfo);
            detail = $"Too many requests. Try again in {seconds} seconds.";
        }

        //Same ProblemDetails pipeline as every other error, so the body carries type and traceId too
        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = detail,
                Extensions = { ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier }
            }
        });
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    //Credential endpoints get a much tighter budget: slows password guessing and mass sign-ups.
    //Applies on top of the global limit, so a login counts against both.
    options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

var versionDescriptionProvider =
    app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in versionDescriptionProvider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json"
                , description.GroupName.ToUpperInvariant());
        }
    });
}

app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseStatusCodePages();
app.UseHttpsRedirection();

//LocalImageRepository writes uploads here and does not create the directory itself,
//and PhysicalFileProvider throws when it is missing - so ensure it exists before both.
var imagesPath = Path.Combine(app.Environment.ContentRootPath, "Images");
Directory.CreateDirectory(imagesPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesPath),
    RequestPath = "/Images"
});

//After static files (image downloads aren't counted), before authentication (rejected requests never reach the JWT handler)
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();

//Top-level statements compile into an internal Program class; making it public lets a test project
//use WebApplicationFactory<Program> to host the real API in memory (Week 3 integration tests)
public partial class Program { }

//Add validation on each filed on Create and Update
//Check if the regionId and difficultyId exist in their tables
//Support filtration for all columns dynamically 
//check if the filterOn column exist and handle its type if its not string 
//Add logging of different levels targeting real scenarion in all controllers
//Define all exception in all controller  for the ExceptionHandlerMiddleware
//Make all cecessary changes in the new version created for the API


// 1. Add centralized validation using FluentValidation (Create + Update DTOs)
//    Example: Reject empty Name, invalid DifficultyId, invalid RegionId before reaching service layer

// 2. Validate foreign keys explicitly
//    Check if RegionId and DifficultyId exist in DB before insert/update
//    Return meaningful 404/400 responses if invalid

// 3. Implement dynamic filtering + sorting + pagination for all GET endpoints
//    Example: /walks?filterOn=name&filterQuery=mountain&sortBy=name&isAscending=true&page=1&pageSize=10

// 4. Safely handle dynamic filter columns
//    Validate filterOn against allowed column whitelist
//    Handle type conversion (string, int, GUID) safely

// 5. Add standardized API response model
//    Example: { success, data, errors, traceId }

// 6. Implement global exception handling using middleware + ProblemDetails (RFC 7807)
//    Ensure all exceptions are mapped to consistent HTTP responses

// 7. Improve ExceptionHandlerMiddleware
//    Log stack traces with Serilog
//    Return user-friendly messages (not raw exceptions)

// 8. Add structured logging across all controllers and services
//    Include: requestId, userId, endpoint, execution time

// 9. Add correlation ID middleware
//    Track each request end-to-end for debugging

// 10. Add API rate limiting
//     - Per IP limits
//     - Per authenticated user limits
//     - Burst protection for abuse prevention

// 11. Add CORS policy configuration
//     - Allow only trusted origins per environment (dev/staging/prod)
//     - Do NOT use AllowAnyOrigin in production

// 12. Containerize the application using Docker
//     - Create Dockerfile
//     - Add docker-compose (API + SQL Server)
//     - Environment-based configuration support

// 13. Add CI/CD pipeline (GitHub Actions or Azure DevOps)
//     - Build
//     - Run tests
//     - Build Docker image
//     - Deploy to staging environment

// 14. Create a separate client project to consume the API
//     - Demonstrate real-world usage (frontend or console app)

// 15. Add health checks and readiness endpoints
//     Example: /health, /ready

// 16. Add caching layer (optional but strong enhancement)
//     - In-memory caching for frequently accessed endpoints
//     - Consider distributed cache (Redis) for scalability

// 17. Add authentication hardening
//     - Review JWT expiry strategy
//     - Secure refresh token flow (if applicable)

// 18. Ensure Swagger is environment-aware and secured in production
