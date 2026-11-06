using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PayrollDashboard.Data;
using PayrollDashboard.Models;

namespace PayrollDashboard.Pages.MyAccount;

[Authorize(Roles = "Employee")]
public sealed class IndexModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public LeaveInput Input { get; set; } = new();

    public Employee? Employee { get; private set; }
    public IReadOnlyList<PayrollEntry> Payslips { get; private set; } = [];
    public IReadOnlyList<LeaveRequest> LeaveRequests { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Employee is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostLeaveAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user?.EmployeeId is not int employeeId
            || !await db.Employees.AnyAsync(employee => employee.Id == employeeId))
        {
            return NotFound();
        }

        var allowedTypes = new[] { "Annual", "Sick", "Unpaid", "Other" };
        if (Input.EndDate < Input.StartDate || !allowedTypes.Contains(Input.LeaveType, StringComparer.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Periksa kembali jenis dan rentang tanggal cuti.");
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var overlaps = await db.LeaveRequests.AnyAsync(request =>
            request.EmployeeId == employeeId
            && request.Status != LeaveStatus.Rejected
            && request.StartDate <= Input.EndDate
            && request.EndDate >= Input.StartDate);
        if (overlaps)
        {
            ModelState.AddModelError(string.Empty, "Sudah ada permohonan cuti pada rentang tanggal tersebut.");
            await LoadAsync();
            return Page();
        }

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employeeId,
            StartDate = Input.StartDate,
            EndDate = Input.EndDate,
            LeaveType = Input.LeaveType
        });
        await db.SaveChangesAsync();
        TempData["Message"] = "Permohonan cuti dikirim untuk ditinjau.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user?.EmployeeId is not int employeeId)
        {
            return;
        }

        Employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(employee => employee.Id == employeeId);
        if (Employee is null)
        {
            return;
        }

        Payslips = await db.PayrollEntries.AsNoTracking()
            .Include(entry => entry.PayrollPeriod)
            .Where(entry => entry.EmployeeId == employeeId
                && (entry.PayrollPeriod.Status == PayrollStatus.Approved
                    || entry.PayrollPeriod.Status == PayrollStatus.Paid))
            .OrderByDescending(entry => entry.PayrollPeriod.EndDate)
            .Take(24)
            .ToListAsync();
        LeaveRequests = await db.LeaveRequests.AsNoTracking()
            .Where(request => request.EmployeeId == employeeId)
            .OrderByDescending(request => request.CreatedAtUtc)
            .Take(50)
            .ToListAsync();
        Message = TempData["Message"] as string;
    }

    public sealed class LeaveInput
    {
        [Required, DataType(DataType.Date)]
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required, DataType(DataType.Date)]
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        public string LeaveType { get; set; } = "Annual";
    }
}
