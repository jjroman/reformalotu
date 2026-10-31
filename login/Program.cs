using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using login.Data;
using login.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.Configure<SqliteVectorOptions>(builder.Configuration.GetSection("SqliteVector"));
builder.Services.AddSingleton<SqliteVectorConnectionInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((provider, options) =>
    options.UseSqlite(connectionString)
        .AddInterceptors(provider.GetRequiredService<SqliteVectorConnectionInterceptor>()));
builder.Services.Configure<VideoCatalogOptions>(builder.Configuration.GetSection("VideoCatalog"));
builder.Services.Configure<VideoInventoryOptions>(builder.Configuration.GetSection("VideoInventory"));
builder.Services.AddVideoAssessment(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<CorpusInventoryReader>();
builder.Services.AddScoped<VideoCatalogImporter>();
builder.Services.AddScoped<VideoRecommendationService>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        options.SignIn.RequireConfirmedAccount = !builder.Environment.IsDevelopment())
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddRazorPages();

builder.Services.AddAuthentication().AddGoogleOpenIdConnect(
    "GoogleOpenIdConnect",
    "Google",
    googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    });

var app = builder.Build();

const string adminRole = "Admin";
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
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

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
