using Microsoft.AspNetCore.Identity;

namespace PayrollDashboard.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";

    public int? EmployeeId { get; set; }
}
