using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Models;
using Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Contexts
{
    public class ShardDbContext : DbContext
    {
        public ShardDbContext(DbContextOptions<ShardDbContext> options) : base(options)
        {
            // Enable detailed logging for troubleshooting
            Database.SetCommandTimeout(30);
        }

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // This should not happen in normal operation, but provides a fallback
                optionsBuilder.EnableSensitiveDataLogging()
                             .EnableDetailedErrors()
                             .LogTo(Console.WriteLine, LogLevel.Information);
            }

            base.OnConfiguring(optionsBuilder);
        }
    }
}
