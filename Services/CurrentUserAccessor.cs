using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace CartStack.Services;

public class CurrentUserAccessor(AuthenticationStateProvider authState)
{
    public async Task<int?> GetUserIdAsync()
    {
        var state = await authState.GetAuthenticationStateAsync();
        var id = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var parsed) ? parsed : null;
    }

    public async Task<string?> GetUserNameAsync()
    {
        var state = await authState.GetAuthenticationStateAsync();
        return state.User.FindFirstValue(ClaimTypes.Name);
    }

    public async Task<int> RequireUserIdAsync()
        => await GetUserIdAsync()
           ?? throw new InvalidOperationException("Not authenticated.");
}
