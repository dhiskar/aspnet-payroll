using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using PayrollDashboard.Data;
using PayrollDashboard.Models;

namespace PayrollDashboard.Pages.Employees;

[Authorize(Roles = "Admin,HR")]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    [ValidateNever]
    public EmployeeInput Input { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public EmployeeInput Edit { get; set; } = new();

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    public IReadOnlyList<Employee> Employees { get; private set; } = [];
    public string? Message { get; private set; }
    public List<string> Errors { get; private set; } = [];

    public async Task OnGetAsync() => await LoadEmployeesAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        ModelState.Clear();
        ValidateEmployeeInput(Input, nameof(Input));
        if (!ModelState.IsValid)
        {
            await LoadEmployeesAsync();
            return Page();
        }

        var exists = await db.Employees.AnyAsync(employee => employee.EmployeeNumber == Input.EmployeeNumber);
        if (exists)
        {
            ModelState.AddModelError("Input.EmployeeNumber", "Nomor karyawan sudah digunakan.");
            await LoadEmployeesAsync();
            return Page();
        }

        db.Employees.Add(new Employee
        {
            EmployeeNumber = Input.EmployeeNumber.Trim().ToUpperInvariant(),
            FullName = Input.FullName.Trim(),
            Department = Input.Department.Trim(),
            Position = Input.Position.Trim(),
            Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
            MonthlySalary = Input.MonthlySalary,
            JoinDate = Input.JoinDate
        });
        await db.SaveChangesAsync();
        TempData["Message"] = "Karyawan berhasil ditambahkan.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (CsvFile is null || CsvFile.Length == 0 || CsvFile.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError(nameof(CsvFile), "Pilih file CSV yang tidak kosong dan berukuran maksimal 5 MB.");
            await LoadEmployeesAsync();
            return Page();
        }

        var imported = new List<Employee>();
        await using var stream = CsvFile.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
        {
            ModelState.AddModelError(nameof(CsvFile), "File CSV tidak memiliki header.");
            await LoadEmployeesAsync();
            return Page();
        }

        var header = ParseCsvLine(headerLine).Select(value => value.Trim()).ToArray();
        var requiredHeaders = new[] { "employeeNumber", "fullName", "department", "position", "email", "monthlySalary", "joinDate" };
        var columns = requiredHeaders.ToDictionary(
            name => name,
            name => Array.FindIndex(header, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)));
        var missing = columns.Where(pair => pair.Value < 0).Select(pair => pair.Key).ToArray();
        if (missing.Length > 0)
        {
            ModelState.AddModelError(nameof(CsvFile), $"Header CSV tidak lengkap. Kolom yang wajib ada: {string.Join(", ", requiredHeaders)}.");
            await LoadEmployeesAsync();
            return Page();
        }

        var rowNumber = 1;
        while (await reader.ReadLineAsync() is { } line)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            string Value(string key) => columns[key] < values.Count ? values[columns[key]].Trim() : "";
            var number = Value("employeeNumber").ToUpperInvariant();
            var name = Value("fullName");
            var salaryText = Value("monthlySalary");
            var dateText = Value("joinDate");
            if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(name)
                || !decimal.TryParse(salaryText, NumberStyles.Number, CultureInfo.InvariantCulture, out var salary)
                || salary is < 0 or > 999_999_999_999m
                || decimal.Round(salary, 2) != salary
                || !DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var joinDate)
                || number.Length > 30
                || name.Length > 150
                || Value("department").Length > 100
                || Value("position").Length > 100
                || Value("email").Length > 254
                || (!string.IsNullOrWhiteSpace(Value("email"))
                    && !new EmailAddressAttribute().IsValid(Value("email"))))
            {
                Errors.Add($"Baris {rowNumber}: employeeNumber, fullName, monthlySalary (angka, tanpa pemisah ribuan), atau joinDate (YYYY-MM-DD) tidak valid.");
                continue;
            }

            imported.Add(new Employee
            {
                EmployeeNumber = number,
                FullName = name,
                Department = Value("department"),
                Position = Value("position"),
                Email = string.IsNullOrWhiteSpace(Value("email")) ? null : Value("email"),
                MonthlySalary = salary,
                JoinDate = joinDate
            });
        }

        var duplicateNumbers = imported
            .GroupBy(employee => employee.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingNumbers = await db.Employees
            .Where(employee => imported.Select(item => item.EmployeeNumber).Contains(employee.EmployeeNumber))
            .Select(employee => employee.EmployeeNumber)
            .ToListAsync();
        var existingSet = existingNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var valid = imported.Where(employee =>
            !duplicateNumbers.Contains(employee.EmployeeNumber) && !existingSet.Contains(employee.EmployeeNumber)).ToList();

        foreach (var duplicate in duplicateNumbers)
        {
            Errors.Add($"Nomor karyawan {duplicate} muncul lebih dari sekali di file.");
        }
        foreach (var duplicate in existingSet)
        {
            Errors.Add($"Nomor karyawan {duplicate} sudah ada.");
        }

        if (valid.Count > 0)
        {
            db.Employees.AddRange(valid);
            await db.SaveChangesAsync();
        }

        TempData["Message"] = $"{valid.Count} karyawan berhasil diimpor; {Errors.Count} baris perlu diperbaiki.";
        await LoadEmployeesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null)
        {
            return NotFound();
        }

        employee.IsActive = !employee.IsActive;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null)
        {
            return NotFound();
        }
        ModelState.Clear();
        ValidateEmployeeInput(Edit, nameof(Edit));
        if (decimal.Round(Edit.MonthlySalary, 2) != Edit.MonthlySalary)
        {
            ModelState.AddModelError("Edit.MonthlySalary", "Gaji hanya boleh memiliki maksimal dua angka desimal.");
        }
        if (!ModelState.IsValid)
        {
            await LoadEmployeesAsync();
            return Page();
        }

        var employeeNumber = Edit.EmployeeNumber.Trim().ToUpperInvariant();
        if (await db.Employees.AnyAsync(item =>
            item.Id != id && item.EmployeeNumber == employeeNumber))
        {
            ModelState.AddModelError("Edit.EmployeeNumber", "Nomor karyawan sudah digunakan.");
            await LoadEmployeesAsync();
            return Page();
        }

        employee.EmployeeNumber = employeeNumber;
        employee.FullName = Edit.FullName.Trim();
        employee.Department = Edit.Department.Trim();
        employee.Position = Edit.Position.Trim();
        employee.Email = string.IsNullOrWhiteSpace(Edit.Email) ? null : Edit.Email.Trim();
        employee.MonthlySalary = Edit.MonthlySalary;
        employee.JoinDate = Edit.JoinDate;
        await db.SaveChangesAsync();
        TempData["Message"] = "Data karyawan berhasil diperbarui. Payroll yang sudah dibuat tidak berubah.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var employees = await db.Employees.AsNoTracking()
            .OrderBy(employee => employee.EmployeeNumber)
            .ToListAsync();
        static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
        var builder = new StringBuilder();
        builder.AppendLine("employeeNumber,fullName,department,position,email,monthlySalary,joinDate,isActive");
        foreach (var employee in employees)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(employee.EmployeeNumber),
                Csv(employee.FullName),
                Csv(employee.Department),
                Csv(employee.Position),
                Csv(employee.Email ?? ""),
                employee.MonthlySalary.ToString(CultureInfo.InvariantCulture),
                employee.JoinDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                employee.IsActive ? "true" : "false"
            }));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "employees.csv");
    }

    private async Task LoadEmployeesAsync()
    {
        Employees = await db.Employees
            .AsNoTracking()
            .OrderBy(employee => employee.EmployeeNumber)
            .ToListAsync();
        Message = TempData["Message"] as string;
    }

    private void ValidateEmployeeInput(EmployeeInput input, string prefix)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), validationResults, validateAllProperties: true);
        foreach (var result in validationResults)
        {
            var members = result.MemberNames.DefaultIfEmpty("");
            foreach (var member in members)
            {
                var key = string.IsNullOrEmpty(member) ? prefix : $"{prefix}.{member}";
                ModelState.AddModelError(key, result.ErrorMessage ?? "Nilai tidak valid.");
            }
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"' && quoted && index + 1 < line.Length && line[index + 1] == '"')
            {
                field.Append('"');
                index++;
            }
            else if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }

    public sealed class EmployeeInput
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(30)]
        public string EmployeeNumber { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(150)]
        public string FullName { get; set; } = "";

        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Department { get; set; } = "";

        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Position { get; set; } = "";

        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string? Email { get; set; }

        [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "999999999999")]
        public decimal MonthlySalary { get; set; }

        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Date)]
        public DateOnly JoinDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }
}
