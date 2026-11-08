using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reformk.Data;

public sealed class VideoCatalogDbContextFactory : IDesignTimeDbContextFactory<VideoCatalogDbContext>
{
    public VideoCatalogDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__VideoCatalog")
            ?? "Data Source=video-catalog.db;Cache=Shared";
        return new VideoCatalogDbContext(
            new DbContextOptionsBuilder<VideoCatalogDbContext>().UseSqlite(connectionString).Options);
    }
}
