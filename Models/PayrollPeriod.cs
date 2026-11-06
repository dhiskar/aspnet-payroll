using System.ComponentModel.DataAnnotations;

namespace PayrollDashboard.Models;

public enum PayrollStatus
{
    Draft,
    InReview,
    Approved,
    Paid
}

public sealed class PayrollPeriod
{
    public int Id { get; set; }

    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly PaymentDate { get; set; }

    public PayrollStatus Status { get; set; }

    public List<PayrollEntry> Entries { get; set; } = [];
}

public sealed class PayrollEntry
{
    public int Id { get; set; }

    public int PayrollPeriodId { get; set; }

    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public int EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    public decimal BaseSalary { get; set; }

    public decimal SalaryAdjustment { get; set; }

    public decimal Allowances { get; set; }

    public decimal OtherDeductions { get; set; }

    public decimal Pph21 { get; set; }

    public decimal BpjsEmployeeContribution { get; set; }

    public decimal NetPay => BaseSalary + SalaryAdjustment + Allowances
        - OtherDeductions - Pph21 - BpjsEmployeeContribution;
}
