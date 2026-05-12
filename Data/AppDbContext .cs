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
    public DbSet<ReservationComment> ReservationComments => Set<ReservationComment>();
    public DbSet<ReservationTripCrew> ReservationTripCrews => Set<ReservationTripCrew>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("timestamp with time zone");
                }
            }
        }

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

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.UpdatedAt)
                .HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<ReservationItem>(entity =>
        {
            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<ReservationHistory>(entity =>
        {
            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamp with time zone");
        });


        modelBuilder.Entity<ReservationComment>(entity =>
        {
            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasOne(x => x.Reservation)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReservationTripCrew>(entity =>
        {
            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasOne(x => x.Reservation)
                .WithMany(x => x.TripCrews)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDateTimesToUtc();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        NormalizeDateTimesToUtc();
        return base.SaveChanges();
    }

    private void NormalizeDateTimesToUtc()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                continue;

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType == typeof(DateTime))
                {
                    var value = (DateTime)property.CurrentValue!;

                    property.CurrentValue = value.Kind switch
                    {
                        DateTimeKind.Utc => value,
                        DateTimeKind.Local => value.ToUniversalTime(),
                        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
                    };
                }

                if (property.Metadata.ClrType == typeof(DateTime?))
                {
                    var value = (DateTime?)property.CurrentValue;

                    if (value.HasValue)
                    {
                        property.CurrentValue = value.Value.Kind switch
                        {
                            DateTimeKind.Utc => value.Value,
                            DateTimeKind.Local => value.Value.ToUniversalTime(),
                            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                        };
                    }
                }
            }
        }
    }
}