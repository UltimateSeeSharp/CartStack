namespace CartStack.Models;

public class GroceryItem
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Qty { get; set; } = 1;
    public string? Notes { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? StoreId { get; set; }
    public Store? Store { get; set; }

    public ItemStatus Status { get; set; } = ItemStatus.Pending;

    public int AddedByUserId { get; set; }
    public User? AddedByUser { get; set; }
    public DateTime AddedAt { get; set; }

    public int? BoughtByUserId { get; set; }
    public User? BoughtByUser { get; set; }
    public DateTime? BoughtAt { get; set; }
}
