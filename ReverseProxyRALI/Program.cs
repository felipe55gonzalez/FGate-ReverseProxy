using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using FGate.Data.Entities;
using FGate.Middlewares;
using FGate.Services;
using Yarp.ReverseProxy.Health;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ProxyDB");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("La cadena de conexión 'ProxyDB' no fue encontrada en appsettings.json.");
}

builder.Services.AddDbContextFactory<ProxyRaliDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddCors(options =>
{
    options.AddPolicy("ProxyCorsPolicy", policyBuilder =>
    {
        var tempServices = builder.Services.BuildServiceProvider();
        using var scope = tempServices.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProxyRaliDbContext>>();
        using var dbContext = dbContextFactory.CreateDbContext();
        try
        {
            var allowedOrigins = dbContext.AllowedCorsOrigins
                                          .Where(o => o.IsEnabled)
                                          .Select(o => o.OriginUrl)
                                          .ToArray();

            if (allowedOrigins.Any())
            {
                policyBuilder.WithOrigins(allowedOrigins)
                             .AllowAnyMethod()
                             .AllowAnyHeader()
                             .AllowCredentials();
            }
            else
            {
                policyBuilder.WithOrigins("http://localhost:INVALID_ORIGIN_BY_DEFAULT")
                             .AllowAnyMethod().AllowAnyHeader().AllowCredentials();
            }
        }
        catch (Exception)
        {
            policyBuilder.WithOrigins("http://localhost:INVALID_ORIGIN_ON_DB_ERROR")
                         .AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        }
    });
});

builder.Services.AddSingleton<IYarpConfigService, DbYarpConfigService>();
builder.Services.AddSingleton<Yarp.ReverseProxy.Configuration.IProxyConfigProvider, DbProxyConfigProvider>();

builder.Services.AddReverseProxy()
    .ConfigureHttpClient((context, handler) =>
    {
        handler.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1);

        handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
    });
builder.Services.AddHostedService<HourlyTrafficAggregatorService>();
builder.Services.AddHostedService<LogCleanupService>();
builder.Services.AddScoped<ITokenService, DbTokenService>();
builder.Services.AddSingleton<IEndpointCategorizer, PathBasedEndpointCategorizer>();
builder.Services.AddSingleton<IAuditLogger, DbAuditLogger>();
builder.Services.AddScoped<IProxyRequestLogger, DbProxyRequestLogger>();
builder.Services.AddScoped<INotificationService, DbNotificationService>();
builder.Services.AddSingleton<IIpBlockingService, DbIpBlockingService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheManagementService, CacheManagementService>();

builder.Services.AddSingleton<ProxyConfigManager>();


builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddAuthentication("AdminCookie")
    .AddCookie("AdminCookie", options =>
    {
        options.Cookie.Name = "FGate.Admin.Auth";
        options.LoginPath = "/Admin/Account/Login";
        options.LogoutPath = "/Admin/Account/Logout";
        options.AccessDeniedPath = "/Admin/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("Administrator", policy =>
        policy.RequireRole("Administrator"));
});

builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Debug));


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Admin/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("ProxyCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/Admin/Home/Index");
        return;
    }
    await next();
});


app.MapControllerRoute(
    name: "adminArea",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.MapReverseProxy(proxyApp =>
{
    proxyApp.UseMiddleware<IpBlockingMiddleware>();
    proxyApp.UseMiddleware<RateLimitingMiddleware>();
    proxyApp.UseMiddleware<WafMiddleware>();
    proxyApp.UseMiddleware<ProxyLoggingMiddleware>();
    proxyApp.UseMiddleware<ProxyTokenValidationMiddleware>();

    proxyApp.UseStatusCodePages(async statusCodeContext =>
    {
        var context = statusCodeContext.HttpContext;
        var response = context.Response;
        if (response.StatusCode == (int)HttpStatusCode.BadGateway ||
            response.StatusCode == (int)HttpStatusCode.ServiceUnavailable)
        {
            response.ContentType = "application/json";
            var errorResponse = new
            {
                message = "El servicio API de backend no está disponible o no responde.",
                status = response.StatusCode,
                requestedPath = context.Request.Path.Value
            };
            await context.Response.WriteAsJsonAsync(errorResponse);
        }
    });
});

app.Run();