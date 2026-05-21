namespace CartStack.Models;

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string IconKey { get; set; }
    public int SortOrder { get; set; }
}
