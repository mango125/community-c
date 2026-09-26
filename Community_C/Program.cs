using Community_C.Controllers;
using Community_C.Models;
using Community_C.Utility;
using Community_C.Utility.Logs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(
    typeof(Program).Assembly,
    optional: true);
builder.AddServerLogging();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register password hasher for User
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Add configuration from Json
// Connection string 
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection 설정이 필요합니다.");

// JWT settings
var jwtSection = builder.Configuration.GetRequiredSection("JwtSettings");
var jwtSettings = jwtSection.Get<JwtSettings>() ?? throw new InvalidOperationException("JwtSettings 설정을 읽을 수 없습니다.");
if (string.IsNullOrWhiteSpace(jwtSettings.Key))
    {    
        throw new InvalidOperationException("JwtSettings:Key 설정이 필요합니다.");
    }
builder.Services.Configure<JwtSettings>(jwtSection); // 의존성 주입

// OAuth 설정
var OAuthSettings = builder.Configuration.GetSection("OAuth");
builder.Services.Configure<OAuthModel.OAuth_Naver>(OAuthSettings.GetSection("Naver"));
builder.Services.Configure<OAuthModel.OAuth_Kakao>(OAuthSettings.GetSection("Kakao"));
builder.Services.Configure<OAuthModel.OAuth_Google>(OAuthSettings.GetSection("Google"));

//https 처리를 위한 ForwardedHeadersOptions 설정
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddDbContext<DataContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHostedService<KeepAlive>();

// JWT 미들웨어 설정
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            JsonWebJoken.CreateAccessTokenValidationParameters(jwtSettings);

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                // JWT 토큰이 만료된 경우 처리
                if (context.Response.StatusCode == 401)
                {
                    context.HandleResponse();
                }
                else
                {
                    await Task.CompletedTask;
                }
            }
        };
    });



//builder.Services.AddControllers();
var app = builder.Build();

ILogger startupLogger = app.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("Community_C.Startup");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataContext>();

    try
    {
        if (await db.Database.CanConnectAsync())
        {
            int boardCount = await db.board.CountAsync();
            ServerLog.DatabaseReady(startupLogger, boardCount);
        }
        else
        {
            ServerLog.DatabaseUnavailable(startupLogger);
        }
    }
    catch (Exception ex)
    {
        ServerLog.DatabaseCheckFailed(startupLogger, ex);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Middleware
app.UseAuthentication();
app.UseServerRequestLogging();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
