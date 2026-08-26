using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.External;
using TCS.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.Configure<ExternalSystemOptions>(builder.Configuration.GetSection(ExternalSystemOptions.SectionName));
builder.Services.AddHttpClient<ExternalSyncClient>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=tcs.db";
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();

// If the browser's login cookie points at a user id that no longer exists in the
// database (e.g. the database was reset/reseeded after they last logged in), every
// controller that calls UserManager.GetUserAsync(User) would otherwise get null back
// and crash with ArgumentNullException deep inside Identity. Catch that here, once,
// and just sign them out cleanly so they land back on the login page instead.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);
        if (user == null)
        {
            var signInManager = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
            await signInManager.SignOutAsync();
            context.Response.Redirect("/Account/Login");
            return;
        }
    }
    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    db.Database.EnsureCreated();

    // This project ships without EF Core migrations. If the model changes (new column,
    // new table) after a tcs.db file already exists on disk, EnsureCreated() will NOT
    // update the existing schema, and any query touching the new column throws at runtime.
    // Guard against that by doing a cheap sanity read across every table/column that has
    // changed; if it fails, the on-disk schema is stale, so wipe and rebuild it.
    try
    {
        _ = db.TrainingSessions.Select(s => new { s.Id, s.Module, s.DepartureTime, s.ReturnTime }).FirstOrDefault();
        _ = db.TrainingConfirmations.Select(c => c.Id).FirstOrDefault();
        _ = db.TransportAssignments.Select(t => new { t.Id, t.ApprovalStatus, t.TrainingSessionId }).FirstOrDefault();
        _ = db.SessionRequests.Select(r => new { r.Id, r.Location, r.LocationType, r.TransportAssignmentId }).FirstOrDefault();
        _ = db.Notifications.Select(n => new { n.Id, n.TargetRole, n.UatProjectId }).FirstOrDefault();
        _ = db.Trainees.Select(t => t.Id).FirstOrDefault();
        _ = db.CompanyBranches.Select(b => b.Id).FirstOrDefault();
        _ = db.UatProjects.Select(p => new { p.Id, p.Status }).FirstOrDefault();
        _ = db.UatSections.Select(s => s.Id).FirstOrDefault();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Existing tcs.db schema is out of date. Recreating the database.");
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    await SeedData.InitializeAsync(services);
}

app.Run();
