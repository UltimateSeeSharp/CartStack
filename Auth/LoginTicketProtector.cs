using Microsoft.AspNetCore.DataProtection;

namespace CartStack.Auth;

public class LoginTicketProtector(IDataProtectionProvider dp)
{
    private readonly IDataProtector _protector = dp.CreateProtector("CartStack.LoginTicket.v1");

    private const int TtlSeconds = 30;

    public string Create(int userId, string userName)
    {
        var payload = $"{userId}|{userName}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        return _protector.Protect(payload);
    }

    public (int UserId, string UserName)? TryConsume(string ticket)
    {
        try
        {
            var payload = _protector.Unprotect(ticket);
            var parts = payload.Split('|');
            if (parts.Length != 3) return null;
            if (!int.TryParse(parts[0], out var userId)) return null;
            if (!long.TryParse(parts[2], out var issuedUnix)) return null;

            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issuedUnix;
            if (age < 0 || age > TtlSeconds) return null;

            return (userId, parts[1]);
        }
        catch
        {
            return null;
        }
    }
}
