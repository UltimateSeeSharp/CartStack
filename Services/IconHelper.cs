using CartStack.Models;
using MudBlazor;

namespace CartStack.Services;

public static class IconHelper
{
    public static string ForCategory(Category? category) =>
        category is null ? Icons.Material.Outlined.Category : ForCategoryKey(category.IconKey);

    public static string ForCategoryKey(string? iconKey) => iconKey switch
    {
        "LocalGroceryStore" => Icons.Material.Filled.LocalGroceryStore,
        "Soap" => Icons.Material.Filled.Soap,
        "Construction" => Icons.Material.Filled.Construction,
        "LocalGasStation" => Icons.Material.Filled.LocalGasStation,
        "LocalPharmacy" => Icons.Material.Filled.LocalPharmacy,
        "Newspaper" => Icons.Material.Filled.Newspaper,
        "BakeryDining" => Icons.Material.Filled.BakeryDining,
        _ => Icons.Material.Outlined.Category,
    };
}
