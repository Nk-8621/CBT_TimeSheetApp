using System.Collections.Generic;

namespace Meridian.Application.DTOs;

/// <summary>One row in a bulk project-allocation request (create-employee or
/// the Resources tab's allocation editor) - the admin picks the project AND,
/// mandatorily, its Classification + BillingCategory together (see
/// BillingClassificationRules). The employee never picks these per timesheet
/// line anymore; TimesheetService derives them from whichever allocation
/// matches the line's project.</summary>
public record EmployeeProjectAllocationInput(int ProjectId, string Classification, string? BillingCategory);

/// <summary>One of an employee's current project allocations, with the
/// admin-set Classification/BillingCategory and enough project detail to
/// display it without a second round trip. Backs GET
/// /api/employees/{employeeCode}/projects.</summary>
public record EmployeeProjectAllocationDto(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    string Classification,
    string? BillingCategory
);
