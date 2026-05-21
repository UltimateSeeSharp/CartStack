using CartStack.Models;
using Microsoft.EntityFrameworkCore;

namespace CartStack.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db, IConfiguration config, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Users.AnyAsync(ct))
        {
            var members = (config["Family:Members"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var name in members)
            {
                db.Users.Add(new User { Name = name });
            }
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Stores.AnyAsync(ct))
        {
            db.Stores.AddRange(
                new Store { Name = "Spar", SortOrder = 0 },
                new Store { Name = "Hofer", SortOrder = 1 },
                new Store { Name = "Baumarkt", SortOrder = 2 });
            await db.SaveChangesAsync(ct);
        }
    }
}
