using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PayrollDashboard.Models;
using PayrollDashboard.Data;

namespace PayrollDashboard.Pages.Users;

[Authorize(Roles = "Admin")]
public sealed class IndexModel(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public NewUserInput Input { get; set; } = new();

    public IReadOnlyList<UserRow> Users { get; private set; } = [];
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public IReadOnlyList<Employee> UnlinkedEmployees { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var knownRole = await roleManager.RoleExistsAsync(Input.Role);
        if (!knownRole)
        {
            ModelState.AddModelError("Input.Role", "Pilih peran yang tersedia.");
        }
        if (Input.Role == "Employee"
            && (Input.EmployeeId is null
                || !await db.Employees.AnyAsync(employee => employee.Id == Input.EmployeeId)
                || await userManager.Users.AnyAsync(user => user.EmployeeId == Input.EmployeeId)))
        {
            ModelState.AddModelError("Input.EmployeeId", "Pilih data karyawan untuk akun Employee.");
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = true,
            DisplayName = Input.DisplayName.Trim(),
            EmployeeId = Input.Role == "Employee" ? Input.EmployeeId : null
        };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadAsync();
            return Page();
        }

        var roleResult = await userManager.AddToRoleAsync(user, Input.Role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadAsync();
            return Page();
        }

        TempData["Message"] = $"Akun {Input.Email} dibuat dengan peran {Input.Role}.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Roles = await roleManager.Roles
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync();
        var linkedEmployeeIds = await userManager.Users
            .Where(user => user.EmployeeId != null)
            .Select(user => user.EmployeeId!.Value)
            .ToListAsync();
        UnlinkedEmployees = await db.Employees.AsNoTracking()
            .Where(employee => !linkedEmployeeIds.Contains(employee.Id))
            .OrderBy(employee => employee.EmployeeNumber)
            .ToListAsync();
        var users = await userManager.Users.OrderBy(user => user.Email).ToListAsync();
        var rows = new List<UserRow>(users.Count);
        foreach (var user in users)
        {
            rows.Add(new UserRow(
                user.Email ?? user.UserName ?? "",
                user.DisplayName,
                string.Join(", ", await userManager.GetRolesAsync(user)),
                user.EmployeeId is null
                    ? ""
                    : await db.Employees.Where(employee => employee.Id == user.EmployeeId)
                        .Select(employee => employee.EmployeeNumber)
                        .FirstOrDefaultAsync() ?? ""));
        }
        Users = rows;
        Message = TempData["Message"] as string;
    }

    public sealed class NewUserInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, StringLength(150)]
        public string DisplayName { get; set; } = "";

        [Required, StringLength(100, MinimumLength = 12), DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        public string Role { get; set; } = "Employee";

        public int? EmployeeId { get; set; }
    }

    public sealed record UserRow(string Email, string DisplayName, string Roles, string EmployeeNumber);
}
