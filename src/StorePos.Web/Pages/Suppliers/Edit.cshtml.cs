using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Suppliers;

[Authorize(Policy = Policies.Manager)]
public class EditModel(SupplierService suppliers) : AppPageModel
{
    [BindProperty] public int? Id { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Contact { get; set; }
    [BindProperty] public string? Phone { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public List<DayOfWeek> Days { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is int sid)
        {
            var s = await suppliers.GetAsync(sid);
            if (s is null) return NotFound();
            (Id, Name, Contact, Phone, Notes, IsActive) = (s.Id, s.Name, s.Contact, s.Phone, s.Notes, s.IsActive);
            Days = s.Schedules.Select(x => x.Day).ToList();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await TryAsync(() => suppliers.SaveAsync(Id, Name, Contact, Phone, Notes, IsActive, Days), "Proveedor guardado."))
            return RedirectToPage("/Suppliers/Index");
        return Page();
    }
}
