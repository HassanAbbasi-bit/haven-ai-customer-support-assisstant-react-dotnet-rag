using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HavenSupport.Infrastructure.Persistence;

public sealed class SupportDbContextFactory : IDesignTimeDbContextFactory<SupportDbContext>
{
    public SupportDbContext CreateDbContext(string[] args)
    {
        // Generating migrations requires no live database or password.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer")
            ?? "Server=127.0.0.1,1433;Database=HavenSupport;Integrated Security=true;Encrypt=true";
        return new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer(connection).Options);
    }
}
