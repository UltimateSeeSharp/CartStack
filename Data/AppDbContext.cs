using CartStack.Models;
using Microsoft.EntityFrameworkCore;

namespace CartStack.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
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

        b.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(64).IsRequired();
            e.Property(c => c.IconKey).HasMaxLength(64).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
        });

        b.Entity<Store>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(64).IsRequired();
            e.Property(s => s.LogoSlug).HasMaxLength(64);
            e.HasIndex(s => new { s.CategoryId, s.Name }).IsUnique();
            e.HasOne(s => s.Category).WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<GroceryItem>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(128).IsRequired();
            e.Property(g => g.Notes).HasMaxLength(512);
            e.HasIndex(g => g.Status);
            e.HasIndex(g => g.BoughtAt);
            e.HasIndex(g => g.CategoryId);
            e.HasOne(g => g.Category).WithMany().HasForeignKey(g => g.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.Store).WithMany().HasForeignKey(g => g.StoreId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.AddedByUser).WithMany().HasForeignKey(g => g.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.BoughtByUser).WithMany().HasForeignKey(g => g.BoughtByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Favorite>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(128).IsRequired();
            e.HasIndex(f => f.Name).IsUnique();
            e.HasOne(f => f.DefaultCategory).WithMany().HasForeignKey(f => f.DefaultCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.DefaultStore).WithMany().HasForeignKey(f => f.DefaultStoreId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
