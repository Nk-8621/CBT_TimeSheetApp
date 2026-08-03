namespace Meridian.Application.Exceptions;

/// <summary>The requested entity doesn't exist. Maps to HTTP 404 in the API layer.</summary>
public class EntityNotFoundException(string entityName, object key)
    : Exception($"{entityName} '{key}' was not found.");

/// <summary>Attempted to edit a week that isn't in Draft/Rejected status. Maps to HTTP 409.</summary>
public class WeekLockedException(string employeeCode, DateOnly weekStartDate, string currentStatus)
    : Exception($"The week of {weekStartDate:yyyy-MM-dd} for {employeeCode} is {currentStatus} and can't be edited.");

/// <summary>A request violated a business rule that isn't a 404 or a lock conflict. Maps to HTTP 400.</summary>
public class BusinessRuleException(string message) : Exception(message);
