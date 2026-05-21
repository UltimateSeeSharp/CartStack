using CartStack.Data;
using Microsoft.EntityFrameworkCore;

namespace CartStack.Services;

public class NameSuggestionCache
{
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(180);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChangeBroadcaster _broadcaster;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private HashSet<string>? _names;

    public NameSuggestionCache(IServiceScopeFactory scopeFactory, ChangeBroadcaster broadcaster)
    {
        _scopeFactory = scopeFactory;
        _broadcaster = broadcaster;
        _broadcaster.Changed += OnChanged;
    }

    private void OnChanged(ChangeEvent e)
    {
        if (e.Kind == ChangeKind.ItemAdded)
        {
            _names = null;
        }
    }

    public async Task<IReadOnlyList<string>> SuggestAsync(string prefix, int max, CancellationToken ct = default)
    {
        var set = await EnsureLoadedAsync(ct);

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return set.Take(max).ToArray();
        }

        return set
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToArray();
    }

    private async Task<HashSet<string>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_names is { } cached) return cached;

        await _lock.WaitAsync(ct);
        try
        {
            if (_names is { } again) return again;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cutoff = DateTime.UtcNow - LookbackWindow;

            var names = await db.GroceryItems
                .Where(g => g.AddedAt >= cutoff)
                .Select(g => g.Name)
                .Distinct()
                .ToListAsync(ct);

            _names = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return _names;
        }
        finally
        {
            _lock.Release();
        }
    }
}
