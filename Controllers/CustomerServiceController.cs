using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCS.Data;
using TCS.Models;
using TCS.Models.ViewModels;

namespace TCS.Controllers;

// Customer Service gets a read-only view of every UAT project a Consultant
// or PM has explicitly forwarded to them - final oversight/archival, not
// another approval gate in the signature chain.
[Authorize(Roles = Roles.CustomerService + "," + Roles.Admin)]
public class CustomerServiceController : Controller
{
    private readonly ApplicationDbContext _db;
    public CustomerServiceController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.UatProjects
            .Where(p => p.SentToCustomerServiceDate != null)
            .Include(p => p.CompanyBranch).ThenInclude(b => b!.Company)
            .Include(p => p.Consultant)
            .Include(p => p.ProjectManager)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.ProjectName.Contains(term)
                || (p.CompanyBranch!.Company!.Name).Contains(term));
        }

        var items = await query.OrderByDescending(p => p.SentToCustomerServiceDate)
            .Select(p => new CsProjectListItem
            {
                UatProjectId = p.Id,
                ProjectName = p.ProjectName,
                CompanyName = p.CompanyBranch!.Company!.Name,
                ConsultantName = p.Consultant!.FullName,
                ProjectManagerName = p.ProjectManager!.FullName,
                ReceivedDate = p.SentToCustomerServiceDate!.Value,
                StatusLabel = p.Status.ToString(),
                StatusCssClass = p.Status.ToString().ToLower()
            }).ToListAsync();

        ViewBag.Search = search;
        return View(items);
    }
}
