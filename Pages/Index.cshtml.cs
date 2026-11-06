using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using PayrollDashboard.Data;
using PayrollDashboard.Models;

namespace PayrollDashboard.Pages;

[Authorize(Roles = "Admin,HR,Finance,Manager")]
public sealed class IndexModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public int ActiveEmployees { get; private set; }
    public int PayrollPeriods { get; private set; }
    public PayrollPeriod? LatestPeriod { get; private set; }
    public decimal LatestNetTotal { get; private set; }
    public string UserRoles { get; private set; } = "";

    public async Task OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is not null)
        {
            UserRoles = string.Join(", ", await userManager.GetRolesAsync(user));
        }

        ActiveEmployees = await db.Employees.CountAsync(employee => employee.IsActive);
        PayrollPeriods = await db.PayrollPeriods.CountAsync();
        LatestPeriod = await db.PayrollPeriods
            .OrderByDescending(period => period.EndDate)
            .FirstOrDefaultAsync();
        if (LatestPeriod is not null)
        {
            LatestNetTotal = await db.PayrollEntries
                .Where(entry => entry.PayrollPeriodId == LatestPeriod.Id)
                .SumAsync(entry => (decimal?)(entry.BaseSalary + entry.SalaryAdjustment
                    + entry.Allowances - entry.OtherDeductions - entry.Pph21
                    - entry.BpjsEmployeeContribution)) ?? 0;
        }
    }
}
