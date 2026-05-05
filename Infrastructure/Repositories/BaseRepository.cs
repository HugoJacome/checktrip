using Microsoft.EntityFrameworkCore;
using CheckTrip.Web.Data;

namespace CheckTrip.Web.Infrastructure.Repositories;

public class BaseRepository<T> where T : class
{
    protected readonly IDbContextFactory<AppDbContext> _dbFactory;

    public BaseRepository(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<T>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Set<T>().ToListAsync();
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Set<T>().FindAsync(id);
    }

    public async Task AddAsync(T entity)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Set<T>().Update(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        db.Set<T>().Remove(entity);
        await db.SaveChangesAsync();
    }
}