using CartStack.Models;
using Microsoft.EntityFrameworkCore;

namespace CartStack.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<GroceryItem> GroceryItems => Set<GroceryItem>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(u => u.Name).HasMaxLength(64).IsRequired();
            e.HasIndex(u => u.Name).IsUnique();
        });

        b.Entity<Store>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(64).IsRequired();
            e.HasIndex(s => s.Name).IsUnique();
        });

        b.Entity<GroceryItem>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(128).IsRequired();
            e.Property(g => g.Notes).HasMaxLength(512);
            e.HasIndex(g => g.Status);
            e.HasIndex(g => g.BoughtAt);
            e.HasOne(g => g.Store).WithMany().HasForeignKey(g => g.StoreId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.AddedByUser).WithMany().HasForeignKey(g => g.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.BoughtByUser).WithMany().HasForeignKey(g => g.BoughtByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Favorite>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(128).IsRequired();
            e.HasIndex(f => f.Name).IsUnique();
            e.HasOne(f => f.DefaultStore).WithMany().HasForeignKey(f => f.DefaultStoreId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
