using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UkManagement.Web.Data;
using UkManagement.Web.Domain;

namespace UkManagement.Web.Services;

public sealed class CurrentResidentService(AppDbContext db)
{
    public async Task<Resident?> GetAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var username = user.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return await db.Residents
            .Include(x => x.Apartment)
            .ThenInclude(x => x.Building)
            .FirstOrDefaultAsync(x => x.KeycloakUsername == username, cancellationToken);
    }

    public async Task<Resident> GetRequiredAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync(user, cancellationToken)
            ?? throw new InvalidOperationException("Для текущего аккаунта не найдена карточка жильца.");
    }
}
