using API.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

public class ConversationDbContext : DbContext
{
    public DbSet<ConversationContext> ConversationContexts { get; set; }

    public ConversationDbContext(DbContextOptions<ConversationDbContext> options) : base(options)
    {
    }
}
