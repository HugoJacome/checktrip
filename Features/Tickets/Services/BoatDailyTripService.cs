using CheckAccess.Infrastructure.Auth;
using CheckTrip.Web.Data;
using CheckTrip.Web.Features.Tickets.Models;
using CheckTrip.Web.Infrastructure.Audit;
using CheckTrip.Web.Infrastructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CheckTrip.Web.Features.Tickets.Services;

public class BoatDailyTripService
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public BoatDailyTripService(
        AppDbContext db,
        ITenantProvider tenant,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<BoatDailyTripResult> GetOrCreateAsync(Guid boatId, DateTime tripDate)
    {
        var tenantId = _tenant.GetTenantId();
        var date = DateOnly.FromDateTime(tripDate.Date);

        var trip = await _db.BoatDailyTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.BoatId == boatId &&
                x.TripDate == date);

        if (trip is null)
        {
            return new BoatDailyTripResult
            {
                BoatId = boatId,
                TripDate = tripDate.Date,
                Status = BoatDailyTripStatus.Open
            };
        }

        return new BoatDailyTripResult
        {
            Id = trip.Id,
            BoatId = trip.BoatId,
            TripDate = trip.TripDate.ToDateTime(TimeOnly.MinValue),
            Status = trip.Status,
            DocumentGeneratedAt = trip.DocumentGeneratedAt,
            DocumentNumber = trip.DocumentNumber,
            DocumentPath = trip.DocumentPath
        };
    }

    public async Task<bool> IsLockedAsync(Guid boatId, DateTime tripDate)
    {
        var tenantId = _tenant.GetTenantId();
        var date = DateOnly.FromDateTime(tripDate.Date);

        return await _db.BoatDailyTrips.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.BoatId == boatId &&
            x.TripDate == date &&
            (x.Status == BoatDailyTripStatus.DocumentGenerated ||
             x.Status == BoatDailyTripStatus.Closed));
    }

    public async Task GenerateDocumentAsync(Guid boatId, DateTime tripDate)
    {
        var tenantId = _tenant.GetTenantId();
        var user = await _currentUser.LoadAsync();

        if (user is null)
            throw new Exception("Usuario no autenticado.");

        var tripDateUtc = DateTime.SpecifyKind(tripDate.Date, DateTimeKind.Utc);
        var date = DateOnly.FromDateTime(tripDateUtc);

        var documentNumber = $"DOC-{tripDateUtc:yyyyMMdd}-{boatId.ToString()[..8]}";

        var trip = await _db.BoatDailyTrips.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.BoatId == boatId &&
            x.TripDate == date);

        if (trip is null)
        {
            trip = new BoatDailyTrip
            {
                TenantId = tenantId,
                BoatId = boatId,
                TripDate = date,
                Status = BoatDailyTripStatus.DocumentGenerated,
                DocumentGeneratedAt = DateTime.UtcNow,
                DocumentGeneratedByUserId = user.UserId,
                DocumentNumber = documentNumber,
                CreatedAt = DateTime.UtcNow
            };

            _db.BoatDailyTrips.Add(trip);
        }
        else
        {
            if (trip.Status == BoatDailyTripStatus.Closed)
                throw new Exception("El viaje ya está cerrado.");

            trip.Status = BoatDailyTripStatus.DocumentGenerated;
            trip.DocumentGeneratedAt = DateTime.UtcNow;
            trip.DocumentGeneratedByUserId = user.UserId;
            trip.DocumentNumber ??= documentNumber;
            trip.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync("BoatDailyTrip", "GenerateDocument", null, new
        {
            BoatId = boatId,
            TripDate = tripDateUtc,
            trip.DocumentNumber
        });
    }

    public async Task CloseAsync(Guid boatId, DateTime tripDate)
    {
        var tenantId = _tenant.GetTenantId();
        var date = DateOnly.FromDateTime(tripDate.Date);

        var trip = await _db.BoatDailyTrips.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.BoatId == boatId &&
            x.TripDate == date);

        if (trip is null)
            throw new Exception("Primero debe generar el documento del viaje.");

        trip.Status = BoatDailyTripStatus.Closed;
        trip.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("BoatDailyTrip", "Close", null, new
        {
            BoatId = boatId,
            TripDate = tripDate.Date
        });
    }
}