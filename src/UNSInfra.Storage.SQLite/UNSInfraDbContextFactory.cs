using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UNSInfra.Storage.SQLite;

/// <summary>
/// Design-time factory for creating UNSInfraDbContext instances for EF migrations
/// </summary>
public class UNSInfraDbContextFactory : IDesignTimeDbContextFactory<UNSInfraDbContext>
{
    public UNSInfraDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UNSInfraDbContext>();
        
        // Use a default SQLite database for migrations
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dbPath = Path.Combine(userHome, ".unsinfra", "unsinfra.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        optionsBuilder.UseSqlite($"Data Source={dbPath};Cache=Shared;Pooling=True;");
        
        return new UNSInfraDbContext(optionsBuilder.Options);
    }
}