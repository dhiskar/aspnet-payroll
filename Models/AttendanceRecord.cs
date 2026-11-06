using System.ComponentModel.DataAnnotations;

namespace PayrollDashboard.Models;

public enum AttendanceStatus
{
    Present,
    Absent,
    Sick,
    Leave
}

public sealed class AttendanceRecord
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    [DataType(DataType.Date)]
    public DateOnly WorkDate { get; set; }

    public AttendanceStatus Status { get; set; }

    [Range(typeof(decimal), "0", "24")]
    public decimal OvertimeHours { get; set; }
}

public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class LeaveRequest
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; }

    [StringLength(30)]
    public string LeaveType { get; set; } = "Annual";

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
