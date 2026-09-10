using Microsoft.EntityFrameworkCore;
using PlcMotorApi.Models;

namespace PlcMotorApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Operation> Operations => Set<Operation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Operation>()
            .HasIndex(o => o.OperationId)
            .IsUnique();
    }
}
