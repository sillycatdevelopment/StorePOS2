using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Models;
using StorePos.Core.Services;
using StorePos.Core.ViewModels;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Inventory;

public class MovementsModel(InventoryService inventory, OwnerService owners) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public int? OwnerId { get; set; }
    [BindProperty(SupportsGet = true)] public MovementType? Type { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<MovementRow> Result { get; private set; } = null!;
    public List<Owner> Owners { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Result = await inventory.GetMovementsAsync(null, OwnerId, Type, From, To, PageNumber);
        Owners = await owners.GetAllAsync();
    }
}
