namespace CartStack.Models;

public class Favorite
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public int? DefaultCategoryId { get; set; }
    public Category? DefaultCategory { get; set; }

    public int? DefaultStoreId { get; set; }
    public Store? DefaultStore { get; set; }

    public int SortOrder { get; set; }
}
