using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PayrollDashboard.Models;

namespace PayrollDashboard.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Employee>()
            .HasIndex(employee => employee.EmployeeNumber)
            .IsUnique();
        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.EmployeeId)
            .IsUnique();
        builder.Entity<ApplicationUser>()
            .HasOne<Employee>()
            .WithOne()
            .HasForeignKey<ApplicationUser>(user => user.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Employee>()
            .Property(employee => employee.MonthlySalary)
            .HasPrecision(18, 2);
        builder.Entity<PayrollPeriod>()
            .HasIndex(period => new { period.StartDate, period.EndDate })
            .IsUnique();
        builder.Entity<PayrollEntry>()
            .HasIndex(entry => new { entry.PayrollPeriodId, entry.EmployeeId })
            .IsUnique();
        builder.Entity<AttendanceRecord>()
            .HasIndex(record => new { record.EmployeeId, record.WorkDate })
            .IsUnique();
        foreach (var property in new[]
                 {
                     nameof(PayrollEntry.BaseSalary),
                     nameof(PayrollEntry.SalaryAdjustment),
                     nameof(PayrollEntry.Allowances),
                     nameof(PayrollEntry.OtherDeductions),
                     nameof(PayrollEntry.Pph21),
                     nameof(PayrollEntry.BpjsEmployeeContribution)
                 })
        {
            builder.Entity<PayrollEntry>().Property<decimal>(property).HasPrecision(18, 2);
        }
        builder.Entity<AttendanceRecord>()
            .Property(record => record.OvertimeHours)
            .HasPrecision(5, 2);
    }
}
