using System.ComponentModel.DataAnnotations;

namespace PayrollDashboard.Models;

public sealed class Employee
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string EmployeeNumber { get; set; } = "";

    [Required, StringLength(150)]
    public string FullName { get; set; } = "";

    [StringLength(100)]
    public string Department { get; set; } = "";

    [StringLength(100)]
    public string Position { get; set; } = "";

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal MonthlySalary { get; set; }

    [DataType(DataType.Date)]
    public DateOnly JoinDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool IsActive { get; set; } = true;
}
