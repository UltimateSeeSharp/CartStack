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

            if (members.Length == 0)
            {
                throw new InvalidOperationException(
                    "Family:Members is empty — no users can be seeded, login dropdown would be empty. " +
                    "Set Family__Members (comma-separated names) as an env var or `fly secrets set`.");
            }

            foreach (var name in members)
            {
                db.Users.Add(new User { Name = name });
            }
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Categories.AnyAsync(ct))
        {
            db.Categories.AddRange(
                new Category { Name = "Lebensmittel", IconKey = "LocalGroceryStore", SortOrder = 0 },
                new Category { Name = "Drogerie", IconKey = "Soap", SortOrder = 1 },
                new Category { Name = "Baumarkt", IconKey = "Construction", SortOrder = 2 },
                new Category { Name = "Tankstelle", IconKey = "LocalGasStation", SortOrder = 3 },
                new Category { Name = "Apotheke", IconKey = "LocalPharmacy", SortOrder = 4 },
                new Category { Name = "Trafik", IconKey = "Newspaper", SortOrder = 5 },
                new Category { Name = "Bäckerei", IconKey = "BakeryDining", SortOrder = 6 });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Stores.AnyAsync(ct))
        {
            var lebensmittel = await db.Categories.FirstAsync(c => c.Name == "Lebensmittel", ct);
            var drogerie = await db.Categories.FirstAsync(c => c.Name == "Drogerie", ct);

            db.Stores.AddRange(
                new Store { Name = "Spar", CategoryId = lebensmittel.Id, LogoSlug = "spar", SortOrder = 0 },
                new Store { Name = "Hofer", CategoryId = lebensmittel.Id, LogoSlug = "hofer", SortOrder = 1 },
                new Store { Name = "Sutterlüty", CategoryId = lebensmittel.Id, LogoSlug = "sutterluety", SortOrder = 2 },
                new Store { Name = "BIPA", CategoryId = drogerie.Id, LogoSlug = "bipa", SortOrder = 0 },
                new Store { Name = "DM", CategoryId = drogerie.Id, LogoSlug = "dm", SortOrder = 1 });
            await db.SaveChangesAsync(ct);
        }
    }
}
