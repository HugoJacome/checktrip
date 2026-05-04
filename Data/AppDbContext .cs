using CheckTrip.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();

    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<BoatRouteSchedule> BoatRouteSchedules => Set<BoatRouteSchedule>();

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Seller> Sellers => Set<Seller>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<ReservationHistory> ReservationHistory => Set<ReservationHistory>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RoleResourcePermission> RoleResourcePermissions => Set<RoleResourcePermission>();
    public DbSet<Ticket> Tickets => Set<Ticket>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(x => x.Id);

            entity.HasMany(x => x.UserRoles)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");

            entity.HasKey(x => x.Id);

            entity.HasMany(x => x.UserRoles)
                .WithOne(x => x.Role)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Permissions)
                .WithOne(x => x.Role)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.UserId, x.RoleId })
                .IsUnique();
        });

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.ToTable("Resources");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasMany(x => x.Permissions)
                .WithOne(x => x.Resource)
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleResourcePermission>(entity =>
        {
            entity.ToTable("RoleResourcePermissions");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.RoleId, x.ResourceId })
                .IsUnique();
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}