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
            .Include(g => g.Category)
            .Include(g => g.Store).ThenInclude(s => s!.Category)
            .Include(g => g.AddedByUser)
            .OrderBy(g => (g.Store != null ? g.Store.Category!.SortOrder : g.Category != null ? g.Category.SortOrder : int.MaxValue))
            .ThenBy(g => g.Store != null ? g.Store.SortOrder : 0)
            .ThenBy(g => g.AddedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<GroceryItem>> GetHistoryAsync(int take = 50, int skip = 0, CancellationToken ct = default)
        => await db.GroceryItems
            .AsNoTracking()
            .Where(g => g.Status == ItemStatus.Bought)
            .Include(g => g.Category)
            .Include(g => g.Store).ThenInclude(s => s!.Category)
            .Include(g => g.AddedByUser)
            .Include(g => g.BoughtByUser)
            .OrderByDescending(g => g.BoughtAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct = default)
        => await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Store>> GetStoresAsync(int? categoryId = null, CancellationToken ct = default)
    {
        var query = db.Stores.AsNoTracking().Include(s => s.Category).AsQueryable();
        if (categoryId is int cid)
        {
            query = query.Where(s => s.CategoryId == cid);
        }
        return await query
            .OrderBy(s => s.Category!.SortOrder)
            .ThenBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Favorite>> GetFavoritesAsync(CancellationToken ct = default)
        => await db.Favorites
            .AsNoTracking()
            .Include(f => f.DefaultCategory)
            .Include(f => f.DefaultStore).ThenInclude(s => s!.Category)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(ct);

    public async Task<GroceryItem> AddItemAsync(string name, int qty, int? categoryId, int? storeId, string? notes = null, CancellationToken ct = default)
    {
        var userId = await currentUser.RequireUserIdAsync();
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Name leer.", nameof(name));

        // If the user picked a store, the store's category is the canonical category.
        int? effectiveCategoryId = categoryId;
        if (storeId is int sid)
        {
            var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct)
                        ?? throw new InvalidOperationException("Geschäft nicht gefunden.");
            effectiveCategoryId = store.CategoryId;
        }

        var item = new GroceryItem
        {
            Name = trimmed,
            Qty = qty <= 0 ? 1 : qty,
            CategoryId = effectiveCategoryId,
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

    public async Task<Store> AddStoreAsync(string name, int categoryId, string? logoSlug = null, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Name leer.", nameof(name));

        var categoryExists = await db.Categories.AnyAsync(c => c.Id == categoryId, ct);
        if (!categoryExists) throw new InvalidOperationException("Kategorie nicht gefunden.");

        var maxOrder = await db.Stores
            .Where(s => s.CategoryId == categoryId)
            .Select(s => (int?)s.SortOrder)
            .MaxAsync(ct) ?? -1;

        var store = new Store
        {
            Name = trimmed,
            CategoryId = categoryId,
            LogoSlug = string.IsNullOrWhiteSpace(logoSlug) ? null : logoSlug.Trim().ToLowerInvariant(),
            SortOrder = maxOrder + 1,
        };
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
        var stores = await db.Stores.Where(s => orderedIds.Contains(s.Id)).ToListAsync(ct);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var s = stores.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (s is not null) s.SortOrder = i;
        }
        await db.SaveChangesAsync(ct);
        broadcaster.Publish(ChangeKind.StoreChanged);
    }

    public async Task<Favorite> AddFavoriteAsync(string name, int? defaultCategoryId, int? defaultStoreId, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Name leer.", nameof(name));

        int? effectiveCategoryId = defaultCategoryId;
        if (defaultStoreId is int sid)
        {
            var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct)
                        ?? throw new InvalidOperationException("Geschäft nicht gefunden.");
            effectiveCategoryId = store.CategoryId;
        }

        var maxOrder = await db.Favorites.Select(f => (int?)f.SortOrder).MaxAsync(ct) ?? -1;
        var fav = new Favorite
        {
            Name = trimmed,
            DefaultCategoryId = effectiveCategoryId,
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
