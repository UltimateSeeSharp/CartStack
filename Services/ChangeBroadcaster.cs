namespace CartStack.Services;

public enum ChangeKind
{
    ItemAdded,
    ItemBought,
    ItemUnbought,
    ItemDeleted,
    StoreChanged,
    FavoriteChanged,
}

public readonly record struct ChangeEvent(ChangeKind Kind, int? Id);

public class ChangeBroadcaster
{
    public event Action<ChangeEvent>? Changed;

    public void Publish(ChangeKind kind, int? id = null)
    {
        Changed?.Invoke(new ChangeEvent(kind, id));
    }
}
