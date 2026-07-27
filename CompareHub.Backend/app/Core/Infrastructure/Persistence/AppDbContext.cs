using Microsoft.EntityFrameworkCore;
using CompareHub.Backend.app.Core.Domain.Entities;

namespace CompareHub.Backend.app.Core.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserSourceLink> UserSourceLinks => Set<UserSourceLink>();
    public DbSet<UserProductSource> UserProductSources => Set<UserProductSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
