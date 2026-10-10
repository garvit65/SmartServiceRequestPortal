namespace ServicePortal.Application.Exceptions;

public class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}

public class WorkflowTransitionException : Exception
{
    public WorkflowTransitionException(string message) : base(message) { }
}

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message = "The record has been modified by another user. Please reload and try again.")
        : base(message) { }
}
