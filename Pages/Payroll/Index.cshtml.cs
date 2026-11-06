using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using PayrollDashboard.Data;
using PayrollDashboard.Models;

namespace PayrollDashboard.Pages.Payroll;

[Authorize(Roles = "Admin,Finance")]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? PeriodId { get; set; }

    public IReadOnlyList<PayrollPeriod> Periods { get; private set; } = [];
    public PayrollPeriod? SelectedPeriod { get; private set; }
    public string? Message { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var end = today.Day <= 20
            ? new DateOnly(today.Year, today.Month, 20)
            : new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(19);
        var start = end.AddMonths(-1).AddDays(1);
        var paymentDate = new DateOnly(end.Year, end.Month, Math.Min(30, DateTime.DaysInMonth(end.Year, end.Month)));

        var existing = await db.PayrollPeriods
            .FirstOrDefaultAsync(period => period.StartDate == start && period.EndDate == end);
        if (existing is not null)
        {
            return RedirectToPage(new { periodId = existing.Id });
        }

        var activeEmployees = await db.Employees
            .Where(employee => employee.IsActive)
            .OrderBy(employee => employee.EmployeeNumber)
            .ToListAsync();
        if (activeEmployees.Count == 0)
        {
            TempData["Message"] = "Tambahkan karyawan aktif sebelum membuat payroll.";
            return RedirectToPage();
        }

        var period = new PayrollPeriod
        {
            StartDate = start,
            EndDate = end,
            PaymentDate = paymentDate,
            Status = PayrollStatus.Draft,
            Entries = activeEmployees.Select(employee => new PayrollEntry
            {
                Employee = employee,
                BaseSalary = employee.MonthlySalary
            }).ToList()
        };
        db.PayrollPeriods.Add(period);
        await db.SaveChangesAsync();
        return RedirectToPage(new { periodId = period.Id });
    }

    public async Task<IActionResult> OnPostUpdateEntryAsync(
        int periodId,
        int entryId,
        decimal salaryAdjustment,
        decimal allowances,
        decimal otherDeductions,
        decimal pph21,
        decimal bpjsEmployeeContribution)
    {
        const decimal maximum = 999_999_999_999m;
        if (salaryAdjustment is < -maximum or > maximum
            || allowances is < 0 or > maximum
            || otherDeductions is < 0 or > maximum
            || pph21 is < 0 or > maximum
            || bpjsEmployeeContribution is < 0 or > maximum)
        {
            TempData["Message"] = "Periksa kembali nominal penyesuaian, tunjangan, dan potongan.";
            return RedirectToPage(new { periodId });
        }

        var entry = await db.PayrollEntries
            .Include(item => item.PayrollPeriod)
            .FirstOrDefaultAsync(item => item.Id == entryId && item.PayrollPeriodId == periodId);
        if (entry is null)
        {
            return NotFound();
        }
        if (entry.PayrollPeriod.Status != PayrollStatus.Draft)
        {
            TempData["Message"] = "Rincian hanya dapat diubah saat status payroll masih Draft.";
            return RedirectToPage(new { periodId });
        }

        if (entry.BaseSalary + salaryAdjustment + allowances
            - otherDeductions - pph21 - bpjsEmployeeContribution < 0)
        {
            TempData["Message"] = "Total potongan tidak boleh melebihi gaji kotor.";
            return RedirectToPage(new { periodId });
        }

        entry.SalaryAdjustment = salaryAdjustment;
        entry.Allowances = allowances;
        entry.OtherDeductions = otherDeductions;
        entry.Pph21 = pph21;
        entry.BpjsEmployeeContribution = bpjsEmployeeContribution;
        await db.SaveChangesAsync();
        TempData["Message"] = "Rincian payroll berhasil disimpan.";
        return RedirectToPage(new { periodId });
    }

    public async Task<IActionResult> OnPostSetPaymentDateAsync(int periodId, DateOnly paymentDate)
    {
        var period = await db.PayrollPeriods.FindAsync(periodId);
        if (period is null)
        {
            return NotFound();
        }
        if (period.Status != PayrollStatus.Draft)
        {
            TempData["Message"] = "Tanggal pembayaran hanya dapat diubah saat status payroll masih Draft.";
            return RedirectToPage(new { periodId });
        }
        if (paymentDate < period.EndDate)
        {
            TempData["Message"] = "Tanggal pembayaran tidak boleh sebelum tanggal akhir periode.";
            return RedirectToPage(new { periodId });
        }

        period.PaymentDate = paymentDate;
        await db.SaveChangesAsync();
        TempData["Message"] = "Tanggal pembayaran diperbarui.";
        return RedirectToPage(new { periodId });
    }

    public async Task<IActionResult> OnPostTransitionAsync(int periodId, PayrollStatus targetStatus)
    {
        var period = await db.PayrollPeriods
            .Include(item => item.Entries)
            .FirstOrDefaultAsync(item => item.Id == periodId);
        if (period is null)
        {
            return NotFound();
        }

        var expectedNext = period.Status switch
        {
            PayrollStatus.Draft => PayrollStatus.InReview,
            PayrollStatus.InReview => PayrollStatus.Approved,
            PayrollStatus.Approved => PayrollStatus.Paid,
            _ => (PayrollStatus?)null
        };
        if (targetStatus != expectedNext)
        {
            TempData["Message"] = "Perubahan status tidak valid. Payroll harus melalui Draft → Tinjau → Disetujui → Dibayar.";
            return RedirectToPage(new { periodId });
        }

        period.Status = targetStatus;
        await db.SaveChangesAsync();
        TempData["Message"] = $"Status payroll sekarang: {targetStatus}.";
        return RedirectToPage(new { periodId });
    }

        public async Task<IActionResult> OnGetExportAsync(int periodId)
        {
            var period = await db.PayrollPeriods.AsNoTracking()
                .Include(item => item.Entries)
                .ThenInclude(entry => entry.Employee)
                .FirstOrDefaultAsync(item => item.Id == periodId);
            if (period is null)
            {
                return NotFound();
            }

            static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
            var builder = new StringBuilder();
            builder.AppendLine("Nomor karyawan,Nama,Departemen,Gaji pokok,Penyesuaian gaji,Tunjangan,Potongan lain,PPh 21,BPJS karyawan,Gaji bersih");
            foreach (var entry in period.Entries.OrderBy(item => item.Employee.EmployeeNumber))
            {
                builder.AppendLine(string.Join(",", new[]
                {
                    Csv(entry.Employee.EmployeeNumber),
                    Csv(entry.Employee.FullName),
                    Csv(entry.Employee.Department),
                    entry.BaseSalary.ToString(CultureInfo.InvariantCulture),
                    entry.SalaryAdjustment.ToString(CultureInfo.InvariantCulture),
                    entry.Allowances.ToString(CultureInfo.InvariantCulture),
                    entry.OtherDeductions.ToString(CultureInfo.InvariantCulture),
                    entry.Pph21.ToString(CultureInfo.InvariantCulture),
                    entry.BpjsEmployeeContribution.ToString(CultureInfo.InvariantCulture),
                    entry.NetPay.ToString(CultureInfo.InvariantCulture)
                }));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8",
                $"payroll-{period.StartDate:yyyyMMdd}-{period.EndDate:yyyyMMdd}.csv");
        }

    private async Task LoadAsync()
    {
        Periods = await db.PayrollPeriods
            .AsNoTracking()
            .OrderByDescending(period => period.EndDate)
            .ToListAsync();
        Message = TempData["Message"] as string;
        if (PeriodId is null && Periods.Count > 0)
        {
            PeriodId = Periods[0].Id;
        }
        if (PeriodId is not null)
        {
            SelectedPeriod = await db.PayrollPeriods
                .AsNoTracking()
                .Include(period => period.Entries)
                .ThenInclude(entry => entry.Employee)
                .FirstOrDefaultAsync(period => period.Id == PeriodId);
        }
    }
}
