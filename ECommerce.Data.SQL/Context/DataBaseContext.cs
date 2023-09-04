using ECommerce.Data.SQL.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Data.SQL.Context
{
    public class DataBaseContext : DbContext
    {
        public DataBaseContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            //builder.ApplyConfiguration(new BookingConfiguration());
        }
        public DbSet<AppUser> AppUsers { get; set; }
    }
}
