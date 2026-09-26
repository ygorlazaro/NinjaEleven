namespace FootballManager.Domain.Common;

/// <summary>
/// Base class for every business error the backend can produce. Services throw these
/// and the API translates them into an HTTP response. The frontend never has to parse
/// free text to discover a rule: it reads <see cref="Code"/>.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// Thrown when an aggregate does not exist. Maps to HTTP 404.
/// </summary>
public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entity, Guid id)
        : base($"{entity}NotFound", $"{entity} '{id}' was not found.")
    {
        Entity = entity;
        Id = id;
    }

    public string Entity { get; }
    public Guid Id { get; }
}

/// <summary>
/// Thrown when a request is well formed but breaks a domain rule. Maps to HTTP 400.
/// </summary>
public class DomainValidationException : DomainException
{
    public DomainValidationException(string code, string message) : base(code, message) { }
}
