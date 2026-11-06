using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using PayrollDashboard.Data;
using PayrollDashboard.Models;

namespace PayrollDashboard.Pages.Operations;

[Authorize(Roles = "Admin,HR,Manager")]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    [ValidateNever]
    public AttendanceInput Attendance { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public LeaveInput Leave { get; set; } = new();

    public IReadOnlyList<Employee> Employees { get; private set; } = [];
    public IReadOnlyList<AttendanceRecord> AttendanceRecords { get; private set; } = [];
    public IReadOnlyList<LeaveRequest> LeaveRequests { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAttendanceAsync()
    {
        ModelState.Clear();
        ValidateInput(Attendance, nameof(Attendance));
        if (!Enum.IsDefined(Attendance.Status)
            || Attendance.OvertimeHours is < 0 or > 24
            || !await db.Employees.AnyAsync(employee => employee.Id == Attendance.EmployeeId))
        {
            ModelState.AddModelError(string.Empty, "Pilih karyawan dan status yang valid; jam lembur harus 0–24.");
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var record = await db.AttendanceRecords.FirstOrDefaultAsync(item =>
            item.EmployeeId == Attendance.EmployeeId && item.WorkDate == Attendance.WorkDate);
        if (record is null)
        {
            record = new AttendanceRecord
            {
                EmployeeId = Attendance.EmployeeId,
                WorkDate = Attendance.WorkDate
            };
            db.AttendanceRecords.Add(record);
        }

        record.Status = Attendance.Status;
        record.OvertimeHours = Attendance.OvertimeHours;
        await db.SaveChangesAsync();
        TempData["Message"] = "Absensi berhasil disimpan.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLeaveAsync()
    {
        ModelState.Clear();
        ValidateInput(Leave, nameof(Leave));
        var leaveTypes = new[] { "Annual", "Sick", "Unpaid", "Other" };
        if (Leave.EndDate < Leave.StartDate
            || !leaveTypes.Contains(Leave.LeaveType, StringComparer.Ordinal)
            || !await db.Employees.AnyAsync(employee => employee.Id == Leave.EmployeeId))
        {
            ModelState.AddModelError(string.Empty, "Periksa karyawan, jenis cuti, dan rentang tanggal.");
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var overlaps = await db.LeaveRequests.AnyAsync(request =>
            request.EmployeeId == Leave.EmployeeId
            && request.Status != LeaveStatus.Rejected
            && request.StartDate <= Leave.EndDate
            && request.EndDate >= Leave.StartDate);
        if (overlaps)
        {
            ModelState.AddModelError(string.Empty, "Karyawan sudah memiliki permohonan cuti pada rentang tanggal tersebut.");
            await LoadAsync();
            return Page();
        }

        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = Leave.EmployeeId,
            StartDate = Leave.StartDate,
            EndDate = Leave.EndDate,
            LeaveType = Leave.LeaveType
        });
        await db.SaveChangesAsync();
        TempData["Message"] = "Permohonan cuti berhasil dicatat.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDecideLeaveAsync(int id, bool approve)
    {
        var request = await db.LeaveRequests.FindAsync(id);
        if (request is null)
        {
            return NotFound();
        }
        if (request.Status != LeaveStatus.Pending)
        {
            TempData["Message"] = "Permohonan ini sudah diproses.";
            return RedirectToPage();
        }

        request.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        await db.SaveChangesAsync();
        TempData["Message"] = approve ? "Permohonan cuti disetujui." : "Permohonan cuti ditolak.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Employees = await db.Employees.AsNoTracking()
            .Where(employee => employee.IsActive)
            .OrderBy(employee => employee.EmployeeNumber)
            .ToListAsync();
        AttendanceRecords = await db.AttendanceRecords.AsNoTracking()
            .Include(record => record.Employee)
            .OrderByDescending(record => record.WorkDate)
            .ThenBy(record => record.Employee.EmployeeNumber)
            .Take(100)
            .ToListAsync();
        LeaveRequests = await db.LeaveRequests.AsNoTracking()
            .Include(request => request.Employee)
            .OrderByDescending(request => request.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
        Message = TempData["Message"] as string;
    }

    private void ValidateInput<T>(T input, string prefix) where T : class
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), validationResults, validateAllProperties: true);
        foreach (var result in validationResults)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(""))
            {
                var key = string.IsNullOrEmpty(member) ? prefix : $"{prefix}.{member}";
                ModelState.AddModelError(key, result.ErrorMessage ?? "Nilai tidak valid.");
            }
        }
    }

    public sealed class AttendanceInput
    {
        [Range(1, int.MaxValue)]
        public int EmployeeId { get; set; }

        [Required, DataType(DataType.Date)]
        public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public AttendanceStatus Status { get; set; }

        [Range(typeof(decimal), "0", "24")]
        public decimal OvertimeHours { get; set; }
    }

    public sealed class LeaveInput
    {
        [Range(1, int.MaxValue)]
        public int EmployeeId { get; set; }

        [Required, DataType(DataType.Date)]
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required, DataType(DataType.Date)]
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        public string LeaveType { get; set; } = "Annual";
    }
}
