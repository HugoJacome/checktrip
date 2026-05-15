using CheckAccess.Components;
using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data;
using CheckTrip.Web.Features.Catalogs.Schedules.Services;
using CheckTrip.Web.Features.Reservations.Services;
using CheckTrip.Web.Features.Security.Services;
using CheckTrip.Web.Features.Tickets.Services;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Auth;
using CheckTrip.Web.Infrastructure.Notifications;
using CheckTrip.Web.Infrastructure.Repositories;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("DefaultConnection")
//    ));

var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnectionPSQL");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
    }));

builder.Services.AddScoped<AgencyRepository>();
builder.Services.AddScoped<AgencyService>(); 
builder.Services.AddScoped<BoatRepository>();
builder.Services.AddScoped<BoatService>();
builder.Services.AddScoped<BoatRouteScheduleRepository>();
builder.Services.AddScoped<BoatRouteScheduleService>();
builder.Services.AddScoped<RouteRepository>();
builder.Services.AddScoped<RouteService>();
builder.Services.AddScoped<SellerRepository>();
builder.Services.AddScoped<SellerService>();
builder.Services.AddScoped<ScheduleRepository>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<TicketRepository>();
builder.Services.AddScoped<TicketService>();

builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditService, AuditService>(); 
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<ReservationRepository>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<BoatDailyTripService>();

builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<SecurityAdminService>();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
    config.SnackbarConfiguration.VisibleStateDuration = 3500;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowTransitionDuration = 300;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.PreventDuplicates = true;
});

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
