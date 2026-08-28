using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.External;
using TCS.Models;
using TCS.Models.ViewModels;

namespace TCS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ExternalSyncClient _sync;

    public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, ExternalSyncClient sync)
    {
        _userManager = userManager;
        _db = db;
        _sync = sync;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        var items = new List<UserListItemViewModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            items.Add(new UserListItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                Role = roles.FirstOrDefault() ?? ""
            });
        }
        return View(items.OrderBy(i => i.Role));
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View(new UserFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Password))
        {
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError(nameof(model.Password), "Password is required for a new user.");
            await PopulateDropdowns();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName,
            TrainerId = model.Role == Roles.Trainer ? model.TrainerId : null,
            CompanyId = model.Role == Roles.ContactPerson ? model.CompanyId : null
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            await PopulateDropdowns();
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);

        // Mirror this login to the instructor's system: Consignee (the
        // person) -> User (the login) -> UserRoleMapper (their role).
        // Does nothing yet until ExternalSystem:BaseUrl is set.
        //
        // If this login belongs to someone who already has a Consignee
        // (e.g. a Trainer added via the roster before getting a login),
        // reuse that same Consignee instead of creating a duplicate person.
        int? consigneeId = null;
        if (model.Role == Roles.Trainer && model.TrainerId is not null)
        {
            var trainer = await _db.Trainers.FindAsync(model.TrainerId);
            if (trainer?.ExternalConsigneeId is not null)
            {
                consigneeId = trainer.ExternalConsigneeId;
                await _sync.UpdateConsigneeAsync(consigneeId.Value, ExternalMapper.ToConsignee(user));
            }
        }
        consigneeId ??= await _sync.SyncConsigneeAndGetIdAsync(ExternalMapper.ToConsignee(user));

        if (consigneeId is not null)
        {
            user.ExternalConsigneeId = consigneeId;
            var userId = await _sync.SyncUserAndGetIdAsync(ExternalMapper.ToUser(user, consigneeId.Value));
            if (userId is not null)
            {
                user.ExternalUserId = userId;
                await _sync.SyncUserRoleMapperAsync(ExternalMapper.ToUserRoleMapper(userId.Value, model.Role));
            }
            await _userManager.UpdateAsync(user);
        }

        TempData["Success"] = "User account created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        var roles = await _userManager.GetRolesAsync(user);

        var model = new UserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault() ?? "",
            TrainerId = user.TrainerId,
            CompanyId = user.CompanyId,
            IsEdit = true
        };
        await PopulateDropdowns();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id!);
        if (user == null) return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns();
            return View(model);
        }

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.TrainerId = model.Role == Roles.Trainer ? model.TrainerId : null;
        user.CompanyId = model.Role == Roles.ContactPerson ? model.CompanyId : null;
        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(model.Role))
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, model.Role);
        }

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, model.Password);
        }

        TempData["Success"] = "User updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user != null) await _userManager.DeleteAsync(user);
        TempData["Success"] = "User removed.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns()
    {
        ViewBag.Roles = Roles.All;
        ViewBag.Trainers = await _db.Trainers.OrderBy(t => t.Name).ToListAsync();
        ViewBag.Companies = await _db.Companies.OrderBy(c => c.Name).ToListAsync();
    }
}
