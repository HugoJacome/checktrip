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
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RoleResourcePermission> RoleResourcePermissions => Set<RoleResourcePermission>();

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<TripRoute> Routes => Set<TripRoute>();
    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<BoatRouteSchedule> BoatRouteSchedules => Set<BoatRouteSchedule>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<ReservationHistory> ReservationHistory => Set<ReservationHistory>();
    public DbSet<ReservationComment> ReservationComments => Set<ReservationComment>();
    public DbSet<ReservationTripCrew> ReservationTripCrews => Set<ReservationTripCrew>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AgencyRouteRate> AgencyRouteRates => Set<AgencyRouteRate>();
    public DbSet<SellerRouteCommission> SellerRouteCommissions => Set<SellerRouteCommission>(); 
    public DbSet<CrewMember> CrewMembers => Set<CrewMember>();
    public DbSet<BoatDailyTrip> BoatDailyTrips => Set<BoatDailyTrip>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureDateTimes(modelBuilder);
        ConfigureSecurity(modelBuilder);
        ConfigureCatalogs(modelBuilder);
        ConfigureReservations(modelBuilder);
        ConfigureAudit(modelBuilder);
    }

    private static void ConfigureDateTimes(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetColumnType("timestamp with time zone");
            }
        }
    }

    private static void ConfigureSecurity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Subdomain).IsUnique();

            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Subdomain).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.NormalizedUsername }).IsUnique();

            entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
            entity.Property(x => x.NormalizedUsername).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.UserRoles)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();

            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.UserRoles)
                .WithOne(x => x.Role)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Permissions)
                .WithOne(x => x.Role)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.ToTable("Resources");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Code).IsUnique();

            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.MenuPath).HasMaxLength(150);
            entity.Property(x => x.Icon).HasMaxLength(100);

            entity.HasMany(x => x.Permissions)
                .WithOne(x => x.Resource)
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.UserId, x.RoleId }).IsUnique();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleResourcePermission>(entity =>
        {
            entity.ToTable("RoleResourcePermissions");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.RoleId, x.ResourceId }).IsUnique();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCatalogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Agency>(entity =>
        {
            entity.ToTable("Agencies");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId);

            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Ruc).HasMaxLength(20);
            entity.Property(x => x.Address).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.ContactPerson).HasMaxLength(150);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.Type).HasMaxLength(50);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Seller>(entity =>
        {
            entity.ToTable("Sellers");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId);

            entity.Property(x => x.DocumentType).HasMaxLength(20);
            entity.Property(x => x.DocumentNumber).HasMaxLength(30);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.Property(x => x.Address).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(150);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TripRoute>(entity =>
        {
            entity.ToTable("Routes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId);

            entity.Property(x => x.Description).HasMaxLength(150);
            entity.Property(x => x.Origin).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Destination).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Place).HasMaxLength(100);
            entity.Property(x => x.Type).HasMaxLength(30);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Boat>(entity =>
        {
            entity.ToTable("Boats");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.TenantId);

            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.RegistrationNumber).HasMaxLength(100);

            entity.Property(x => x.OwnerName).HasMaxLength(200);
            entity.Property(x => x.OwnerRuc).HasMaxLength(30);
            entity.Property(x => x.OwnerEmail).HasMaxLength(150);
            entity.Property(x => x.OwnerPhone).HasMaxLength(50);

        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.ToTable("Schedules");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId);

            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BoatRouteSchedule>(entity =>
        {
            entity.ToTable("BoatRouteSchedules");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.BoatId, x.RouteId, x.ScheduleId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.RouteId, x.ScheduleId });

            entity.Property(x => x.Days).HasMaxLength(7);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.Color).HasMaxLength(20);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Boat)
                .WithMany()
                .HasForeignKey(x => x.BoatId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Route)
                .WithMany()
                .HasForeignKey(x => x.RouteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Schedule)
                .WithMany()
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgencyRouteRate>(entity =>
        {
            entity.ToTable("AgencyRouteRates");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.AgencyId);
            entity.HasIndex(x => x.RouteId);

            entity.HasIndex(x => new { x.TenantId, x.AgencyId, x.RouteId })
                .IsUnique();

            entity.Property(x => x.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Agency)
                .WithMany(x => x.RouteRates)
                .HasForeignKey(x => x.AgencyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Route)
                .WithMany()
                .HasForeignKey(x => x.RouteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SellerRouteCommission>(entity =>
        {
            entity.ToTable("SellerRouteCommissions");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.SellerId);
            entity.HasIndex(x => x.RouteId);

            entity.HasIndex(x => new { x.TenantId, x.SellerId, x.RouteId })
                .IsUnique();

            entity.Property(x => x.Commission)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Seller)
                .WithMany(x => x.RouteCommissions)
                .HasForeignKey(x => x.SellerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Route)
                .WithMany()
                .HasForeignKey(x => x.RouteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CrewMember>(entity =>
        {
            entity.ToTable("CrewMembers");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.BoatId);
            entity.HasIndex(x => new { x.TenantId, x.BoatId, x.IsActive });

            entity.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.DocumentNumber).HasMaxLength(30);
            entity.Property(x => x.Phone).HasMaxLength(50);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Boat)
                .WithMany(x => x.CrewMembers)
                .HasForeignKey(x => x.BoatId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureReservations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.DocumentNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.FullName });

            entity.Property(x => x.DocumentType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DocumentNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Nationality).HasMaxLength(100);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservations");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.ReservationCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => x.AgencyId);
            entity.HasIndex(x => x.SellerId);

            entity.Property(x => x.ReservationCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ExternalReference).HasMaxLength(50);
            entity.Property(x => x.ContactName).HasMaxLength(150);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PaymentStatus).HasMaxLength(30).IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Agency)
                .WithMany()
                .HasForeignKey(x => x.AgencyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Seller)
                .WithMany()
                .HasForeignKey(x => x.SellerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Items)
                .WithOne(x => x.Reservation)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Comments)
                .WithOne(x => x.Reservation)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.TripCrews)
                .WithOne(x => x.Reservation)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReservationItem>(entity =>
        {
            entity.ToTable("ReservationItems");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.ReservationId);
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => new { x.TenantId, x.OutboundRouteScheduleId, x.OutboundTravelDate, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.ReturnRouteScheduleId, x.ReturnTravelDate, x.Status });

            entity.Property(x => x.TripType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PassengerType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.Discount).HasPrecision(18, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Reservation)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OutboundRouteSchedule)
                .WithMany()
                .HasForeignKey(x => x.OutboundRouteScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReturnRouteSchedule)
                .WithMany()
                .HasForeignKey(x => x.ReturnRouteScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReservationHistory>(entity =>
        {
            entity.ToTable("ReservationHistory");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.ReservationId });

            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(250);
            entity.Property(x => x.OldStatus).HasMaxLength(30);
            entity.Property(x => x.NewStatus).HasMaxLength(30);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Reservation)
                .WithMany()
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReservationComment>(entity =>
        {
            entity.ToTable("ReservationComments");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.ReservationId });

            entity.Property(x => x.CommentType).IsRequired();
            entity.Property(x => x.Comment).IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Reservation)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReservationTripCrew>(entity =>
        {
            entity.ToTable("ReservationTripCrews");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.ReservationId });
            entity.HasIndex(x => new { x.ReservationId, x.TripType });
            entity.HasIndex(x => x.BoatId);

            entity.Property(x => x.TripType).IsRequired();
            entity.Property(x => x.CaptainName).IsRequired();

            entity.HasOne(x => x.Reservation)
                .WithMany(x => x.TripCrews)
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Boat)
                .WithMany(x => x.TripCrews)
                .HasForeignKey(x => x.BoatId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.ReservationItemId);
            entity.HasIndex(x => new { x.TenantId, x.TicketNumber }).IsUnique();

            entity.Property(x => x.TicketNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TicketType).HasMaxLength(30);
            entity.Property(x => x.Color).HasMaxLength(20);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReservationItem)
                .WithMany()
                .HasForeignKey(x => x.ReservationItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ReservationItem)
                .WithMany()
                .HasForeignKey(x => x.ReservationItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Boat)
                .WithMany()
                .HasForeignKey(x => x.BoatId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BoatDailyTrip>(entity =>
        {
            entity.ToTable("BoatDailyTrips");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.BoatId, x.TripDate }).IsUnique();

            entity.Property(x => x.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.DocumentNumber)
                .HasMaxLength(50);

            entity.Property(x => x.DocumentPath)
                .HasMaxLength(500);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Boat)
                .WithMany()
                .HasForeignKey(x => x.BoatId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAudit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => new { x.EntityName, x.EntityId });

            entity.Property(x => x.EntityName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(100);
            entity.Property(x => x.IpAddress).HasMaxLength(50);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
        });
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