using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

public class ConversationDbContextFactory : IDesignTimeDbContextFactory<ConversationDbContext>
{
    public ConversationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConversationDbContext>();

        // Build configuration
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // Get connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Configure the DbContext to use SQL Server

        optionsBuilder.UseSqlServer(connectionString);

        return new ConversationDbContext(optionsBuilder.Options);
    }
}
