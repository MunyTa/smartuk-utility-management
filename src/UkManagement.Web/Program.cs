using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using UkManagement.Web.Data;
using UkManagement.Web.Endpoints;
using UkManagement.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var authenticationEnabled = builder.Configuration.GetValue("Authentication:Enabled", true);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
    options.UseNpgsql(connectionString);
});

builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection("Sms"));
builder.Services.Configure<VapidOptions>(builder.Configuration.GetSection("Vapid"));
builder.Services.Configure<KeycloakAdminOptions>(builder.Configuration.GetSection("KeycloakAdmin"));
builder.Services.Configure<SimulatorCatalogOptions>(builder.Configuration.GetSection("SimulatorCatalog"));

builder.Services.AddScoped<MeterReadingIngestionService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<PushNotificationService>();
builder.Services.AddScoped<CurrentResidentService>();
builder.Services.AddScoped<MeterProvisioningService>();
builder.Services.AddScoped<IClaimsTransformation, KeycloakRoleClaimsTransformation>();
builder.Services.AddScoped<MeterReadingRetentionService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddHttpClient<ISmsSender, SmsSender>();
builder.Services.AddHttpClient<KeycloakAdminService>();
builder.Services.AddHostedService<MqttReadingConsumer>();

builder.Services.AddRazorPages(options =>
{
    if (authenticationEnabled)
    {
        options.Conventions.AuthorizeFolder("/");
        options.Conventions.AuthorizeFolder("/Meters", "Admin");
        options.Conventions.AuthorizeFolder("/Readings", "Admin");
        options.Conventions.AuthorizeFolder("/Requests", "Staff");
        options.Conventions.AuthorizeFolder("/Notifications", "Staff");
        options.Conventions.AuthorizeFolder("/Audit", "Admin");
        options.Conventions.AuthorizeFolder("/Reports", "Admin");
        options.Conventions.AuthorizeFolder("/Dispatcher", "Dispatcher");
        options.Conventions.AuthorizeFolder("/Resident", "Resident");
        options.Conventions.AuthorizePage("/Admin/Index", "Admin");
        options.Conventions.AuthorizeFolder("/Admin/Residents", "Admin");
        options.Conventions.AuthorizeFolder("/Admin/Apartments", "Admin");
        options.Conventions.AuthorizeFolder("/Admin/Registrations", "Admin");
        options.Conventions.AllowAnonymousToPage("/Index");
        options.Conventions.AllowAnonymousToFolder("/Register");
        options.Conventions.AllowAnonymousToPage("/Error");
    }
});

if (authenticationEnabled)
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            var publicAuthority = builder.Configuration["Authentication:PublicAuthority"];
            options.Authority = builder.Configuration["Authentication:Authority"];
            options.MetadataAddress = builder.Configuration["Authentication:MetadataAddress"];
            options.ClientId = builder.Configuration["Authentication:ClientId"];
            if (!string.IsNullOrWhiteSpace(options.MetadataAddress)
                && !string.IsNullOrWhiteSpace(options.Authority)
                && !string.IsNullOrWhiteSpace(publicAuthority))
            {
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    options.MetadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new RewritingOpenIdConnectDocumentRetriever(publicAuthority, options.Authority));
            }
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.RequireHttpsMetadata = false;
            options.BackchannelTimeout = TimeSpan.FromSeconds(10);
            options.SaveTokens = true;
            options.UsePkce = true;
            options.MapInboundClaims = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.ClaimActions.MapJsonKey("role", "role");
            options.ClaimActions.MapJsonKey("roles", "roles");
            options.ClaimActions.MapJsonKey("realm_access", "realm_access");
            options.ClaimActions.MapJsonKey("resource_access", "resource_access");
            options.TokenValidationParameters.NameClaimType = "preferred_username";
            options.TokenValidationParameters.RoleClaimType = "role";
            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    context.ProtocolMessage.UiLocales = "ru";
                    context.ProtocolMessage.SetParameter("kc_locale", "ru");

                    if (!string.IsNullOrWhiteSpace(publicAuthority))
                    {
                        context.ProtocolMessage.IssuerAddress =
                            $"{publicAuthority.TrimEnd('/')}/protocol/openid-connect/auth";
                    }

                    return Task.CompletedTask;
                },
                OnRedirectToIdentityProviderForSignOut = context =>
                {
                    context.ProtocolMessage.SetParameter("ui_locales", "ru");
                    context.ProtocolMessage.SetParameter("kc_locale", "ru");

                    if (!string.IsNullOrWhiteSpace(publicAuthority))
                    {
                        context.ProtocolMessage.IssuerAddress =
                            $"{publicAuthority.TrimEnd('/')}/protocol/openid-connect/logout";
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Staff", policy =>
            policy.RequireRole("Admin", "Dispatcher"));
        options.AddPolicy("Admin", policy =>
            policy.RequireRole("Admin"));
        options.AddPolicy("Dispatcher", policy =>
            policy.RequireRole("Dispatcher"));
        options.AddPolicy("Resident", policy =>
            policy.RequireRole("Resident"));
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

if (authenticationEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/login", (HttpContext context) =>
        Results.Challenge(new AuthenticationProperties { RedirectUri = "/" },
            [OpenIdConnectDefaults.AuthenticationScheme])).AllowAnonymous();

    app.MapPost("/logout", () =>
    {
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIdConnectDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme]);
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "uk-management-web" }))
    .AllowAnonymous();

app.MapPushEndpoints();
app.MapSimulatorEndpoints();
app.MapRazorPages();

app.Run();

public partial class Program;
