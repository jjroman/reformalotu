using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Reformk.Data;
using Reformk.Identity;
using Reformk.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var catalogConnectionString = builder.Configuration.GetConnectionString("VideoCatalog");
if (string.IsNullOrWhiteSpace(catalogConnectionString))
{
    var catalogBuilder = new SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(catalogBuilder.DataSource) || catalogBuilder.DataSource == ":memory:")
    {
        throw new InvalidOperationException("Configure ConnectionStrings:VideoCatalog for a separate, persistent SQLite database.");
    }
    catalogBuilder.DataSource = Path.Combine(Path.GetDirectoryName(catalogBuilder.DataSource) ?? string.Empty, "video.db");
    catalogConnectionString = catalogBuilder.ToString();
}
builder.Services.Configure<SqliteVectorOptions>(builder.Configuration.GetSection("SqliteVector"));
builder.Services.AddSingleton<SqliteVectorConnectionInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((provider, options) =>
    options.UseSqlite(connectionString, sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
builder.Services.AddDbContext<VideoCatalogDbContext>((provider, options) =>
    options.UseSqlite(catalogConnectionString, sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .AddInterceptors(provider.GetRequiredService<SqliteVectorConnectionInterceptor>()));
builder.Services.Configure<VideoCatalogOptions>(builder.Configuration.GetSection("VideoCatalog"));
builder.Services.AddSemanticSearch(builder.Configuration, builder.Environment);
builder.Services.AddProjectAi(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<CorpusInventoryReader>();
builder.Services.AddScoped<VideoCatalogImporter>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var mailSenderOptions = builder.Services.AddOptions<MailSenderOptions>()
    .Bind(builder.Configuration.GetSection(MailSenderOptions.SectionName))
    .ValidateDataAnnotations();
if (!builder.Environment.IsDevelopment())
{
    mailSenderOptions.ValidateOnStart();
}

builder.Services.AddTransient<MailerSendIdentityEmailSender>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.IEmailSender<IdentityUser>>(
    services => services.GetRequiredService<MailerSendIdentityEmailSender>());
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>(
    services => services.GetRequiredService<MailerSendIdentityEmailSender>());

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        options.SignIn.RequireConfirmedAccount = !builder.Environment.IsDevelopment())
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped<IdentityErrorDescriber, SpanishIdentityErrorDescriber>();
builder.Services.AddRazorPages();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var spanish = new CultureInfo("es");
    options.DefaultRequestCulture = new RequestCulture(spanish);
    options.SupportedCultures = [spanish];
    options.SupportedUICultures = [spanish];
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var hasGoogleClientId = !string.IsNullOrWhiteSpace(googleClientId);
var hasGoogleClientSecret = !string.IsNullOrWhiteSpace(googleClientSecret);
var googleSignInConfigured = hasGoogleClientId && hasGoogleClientSecret;

if (googleSignInConfigured)
{
    builder.Services.AddAuthentication().AddGoogleOpenIdConnect(
        "GoogleOpenIdConnect",
        "Google",
        googleOptions =>
        {
            googleOptions.ClientId = googleClientId;
            googleOptions.ClientSecret = googleClientSecret;
        });
}

var app = builder.Build();

if (!googleSignInConfigured)
{
    if (hasGoogleClientId || hasGoogleClientSecret)
    {
        app.Logger.LogWarning(
            "Google sign-in is disabled because both Authentication:Google:ClientId and Authentication:Google:ClientSecret must be configured.");
    }
    else
    {
        app.Logger.LogInformation("Google sign-in is disabled because its credentials are not configured.");
    }
}

const string adminRole = "Admin";
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var catalogContext = scope.ServiceProvider.GetRequiredService<VideoCatalogDbContext>();
    await VideoCatalogDatabaseMigration.MigrateAsync(dbContext, catalogContext);
    await scope.ServiceProvider.GetRequiredService<VideoCatalogImporter>().ImportAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the {adminRole} role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
        }
    }

    var bootstrapAdminEmail = app.Configuration["BootstrapAdmin:Email"];
    if (!string.IsNullOrWhiteSpace(bootstrapAdminEmail))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByEmailAsync(bootstrapAdminEmail);
        if (user is null)
        {
            app.Logger.LogWarning("Bootstrap admin account was not found. It will be assigned the Admin role after it is registered.");
        }
        else if (!await userManager.IsInRoleAsync(user, adminRole))
        {
            var userResult = await userManager.AddToRoleAsync(user, adminRole);
            if (!userResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign the {adminRole} role: {string.Join("; ", userResult.Errors.Select(error => error.Description))}");
            }

            app.Logger.LogInformation("Assigned the {Role} role to the configured bootstrap account.", adminRole);
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
