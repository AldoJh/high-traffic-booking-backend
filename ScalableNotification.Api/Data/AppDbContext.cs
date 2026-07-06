using Microsoft.EntityFrameworkCore;
using ScalableNotification.Api.Models;

namespace ScalableNotification.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Notificationlogs> Notificationlogs { get; set; }
    }
}