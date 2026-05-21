using CartStack.Models;

namespace CartStack.Services;

public interface IGroceryService
{
    Task<IReadOnlyList<GroceryItem>> GetPendingItemsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GroceryItem>> GetHistoryAsync(int take = 50, int skip = 0, CancellationToken ct = default);

    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Store>> GetStoresAsync(int? categoryId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Favorite>> GetFavoritesAsync(CancellationToken ct = default);

    Task<GroceryItem> AddItemAsync(string name, int qty, int? categoryId, int? storeId, string? notes = null, CancellationToken ct = default);
    Task MarkBoughtAsync(int itemId, CancellationToken ct = default);
    Task UnmarkBoughtAsync(int itemId, CancellationToken ct = default);
    Task DeleteItemAsync(int itemId, CancellationToken ct = default);

    Task<Store> AddStoreAsync(string name, int categoryId, string? logoSlug = null, CancellationToken ct = default);
    Task RenameStoreAsync(int storeId, string name, CancellationToken ct = default);
    Task DeleteStoreAsync(int storeId, CancellationToken ct = default);
    Task ReorderStoresAsync(IReadOnlyList<int> orderedIds, CancellationToken ct = default);

    Task<Favorite> AddFavoriteAsync(string name, int? defaultCategoryId, int? defaultStoreId, CancellationToken ct = default);
    Task DeleteFavoriteAsync(int favoriteId, CancellationToken ct = default);
    Task ReorderFavoritesAsync(IReadOnlyList<int> orderedIds, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetNameSuggestionsAsync(string prefix, int max = 5, CancellationToken ct = default);
}
