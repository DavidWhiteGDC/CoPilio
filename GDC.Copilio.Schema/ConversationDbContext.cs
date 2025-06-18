using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GDC.Copilio.Entities.Models;

namespace GDC.Copilio.Schema
{
    public class ConversationDbContext : DbContext
    {
       
            public DbSet<Conversation> ConversationMemory { get; set; }

            public ConversationDbContext(DbContextOptions<ConversationDbContext> options) : base(options)
            {
            }
       

    }
}
