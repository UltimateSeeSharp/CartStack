using CartStack.Data;
using CartStack.Models;
using Microsoft.EntityFrameworkCore;

namespace CartStack.Services;

public class GroceryService(
    AppDbContext db,
    CurrentUserAccessor currentUser,
    ChangeBroadcaster broadcaster,
    NameSuggestionCache suggestions) : IGroceryService
{
    public async Task<IReadOnlyList<GroceryItem>> GetPendingItemsAsync(CancellationToken ct = default)
        => await db.GroceryItems
            .AsNoTracking()
            .Where(g => g.Status == ItemStatus.Pending)
            .Include(g => g.Store)
            .Include(g => g.AddedByUser)
            .OrderBy(g => g.Store!.SortOrder).ThenBy(g => g.AddedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<GroceryItem>> GetHistoryAsync(int take = 50, int skip = 0, CancellationToken ct = default)
        => await db.GroceryItems
            .AsNoTracking()
            .Where(g => g.Status == ItemStatus.Bought)
            .Include(g => g.Store)
            .Include(g => g.AddedByUser)
            .Include(g => g.BoughtByUser)
            .OrderByDescending(g => g.BoughtAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken ct = default)
        => await db.Stores
            .AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Favorite>> GetFavoritesAsync(CancellationToken ct = default)
        => await db.Favorites
            .AsNoTracking()
            .Include(f => f.DefaultStore)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(ct);

    public async Task<GroceryItem> AddItemAsync(string name, int qty, int storeId, string? notes = null, CancellationToken ct = default)
    {
        var userId = await currentUser.RequireUserIdAsync();
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Name leer.", nameof(name));

        var item = new GroceryItem
        {
            Name = trimmed,
            Qty = qty <= 0 ? 1 : qty,
            StoreId = storeId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = ItemStatus.Pending,
            AddedByUserId = userId,
            AddedAt = DateTime.UtcNow,
        };

        db.GroceryItems.Add(item);
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.ItemAdded, item.Id);
        return item;
    }

    public async Task MarkBoughtAsync(int itemId, CancellationToken ct = default)
    {
        var userId = await currentUser.RequireUserIdAsync();
        var item = await db.GroceryItems.FirstOrDefaultAsync(g => g.Id == itemId, ct)
                   ?? throw new InvalidOperationException("Item nicht gefunden.");

        if (item.Status == ItemStatus.Bought) return;

        item.Status = ItemStatus.Bought;
        item.BoughtByUserId = userId;
        item.BoughtAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.ItemBought, item.Id);
    }

    public async Task UnmarkBoughtAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.GroceryItems.FirstOrDefaultAsync(g => g.Id == itemId, ct)
                   ?? throw new InvalidOperationException("Item nicht gefunden.");

        if (item.Status == ItemStatus.Pending) return;

        item.Status = ItemStatus.Pending;
        item.BoughtByUserId = null;
        item.BoughtAt = null;
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.ItemUnbought, item.Id);
    }

    public async Task DeleteItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.GroceryItems.FirstOrDefaultAsync(g => g.Id == itemId, ct);
        if (item is null) return;

        db.GroceryItems.Remove(item);
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.ItemDeleted, itemId);
    }

    public async Task<Store> AddStoreAsync(string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Name leer.", nameof(name));

        var maxOrder = await db.Stores.Select(s => (int?)s.SortOrder).MaxAsync(ct) ?? -1;
        var store = new Store { Name = trimmed, SortOrder = maxOrder + 1 };
        db.Stores.Add(store);
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.StoreChanged, store.Id);
        return store;
    }

    public async Task RenameStoreAsync(int storeId, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Name leer.", nameof(name));

        var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct)
                    ?? throw new InvalidOperationException("Geschäft nicht gefunden.");

        store.Name = trimmed;
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.StoreChanged, storeId);
    }

    public async Task DeleteStoreAsync(int storeId, CancellationToken ct = default)
    {
        var inUse = await db.GroceryItems.AnyAsync(g => g.StoreId == storeId, ct)
                    || await db.Favorites.AnyAsync(f => f.DefaultStoreId == storeId, ct);
        if (inUse) throw new InvalidOperationException("Geschäft wird noch verwendet.");

        var store = await db.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null) return;

        db.Stores.Remove(store);
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.StoreChanged, storeId);
    }

    public async Task ReorderStoresAsync(IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        var stores = await db.Stores.ToListAsync(ct);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var s = stores.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (s is not null) s.SortOrder = i;
        }
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.StoreChanged);
    }

    public async Task<Favorite> AddFavoriteAsync(string name, int defaultStoreId, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Name leer.", nameof(name));

        var maxOrder = await db.Favorites.Select(f => (int?)f.SortOrder).MaxAsync(ct) ?? -1;
        var fav = new Favorite
        {
            Name = trimmed,
            DefaultStoreId = defaultStoreId,
            SortOrder = maxOrder + 1,
        };
        db.Favorites.Add(fav);
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.FavoriteChanged, fav.Id);
        return fav;
    }

    public async Task DeleteFavoriteAsync(int favoriteId, CancellationToken ct = default)
    {
        var fav = await db.Favorites.FirstOrDefaultAsync(f => f.Id == favoriteId, ct);
        if (fav is null) return;

        db.Favorites.Remove(fav);
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.FavoriteChanged, favoriteId);
    }

    public async Task ReorderFavoritesAsync(IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        var favs = await db.Favorites.ToListAsync(ct);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var f = favs.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (f is not null) f.SortOrder = i;
        }
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.FavoriteChanged);
    }

    public Task<IReadOnlyList<string>> GetNameSuggestionsAsync(string prefix, int max = 5, CancellationToken ct = default)
        => suggestions.SuggestAsync(prefix, max, ct);
}
