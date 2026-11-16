using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Settings;

[Authorize(Policy = Policies.Admin)]
public class UsersModel(UserService users, DeletionService deletion) : AppPageModel
{
    public List<User> Users { get; private set; } = new();

    public async Task OnGetAsync() => Users = await users.GetAllAsync();

    public async Task<IActionResult> OnPostCreateAsync(string username, string displayName, string password, UserRole role)
    {
        await TryAsync(() => users.CreateAsync(username, displayName, password, role), "Usuario creado.", redirecting: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, string displayName, UserRole role, bool isActive, string? newPassword)
    {
        await TryAsync(() => users.UpdateAsync(id, displayName, role, isActive, newPassword), "Usuario actualizado.", redirecting: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await TryAsync(() => deletion.DeleteUserAsync(id), "Usuario eliminado.", redirecting: true);
        return RedirectToPage();
    }
}
