using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Owners;

[Authorize(Policy = Policies.Manager)]
public class EditModel(OwnerService owners) : AppPageModel
{
    [BindProperty] public int? Id { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Contact { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is int oid)
        {
            var o = await owners.GetAsync(oid);
            if (o is null) return NotFound();
            (Id, Name, Contact, Notes, IsActive) = (o.Id, o.Name, o.Contact, o.Notes, o.IsActive);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await TryAsync(() => owners.SaveAsync(Id, Name, Contact, Notes, IsActive), "Dueño guardado."))
            return RedirectToPage("/Owners/Index");
        return Page();
    }
}
