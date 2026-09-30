namespace NinjaEleven.Application.Abstractions;

/// <summary>
/// Which process this is.
///
/// A match's working state lives in the memory of the process that is playing it, and the
/// world is now played by more than one: the Scheduler moves the matches nobody is watching
/// and the API moves the one somebody is. A row therefore has to record which of them holds
/// a match, and this is where that name comes from — generated once per process, so two
/// processes never answer the same.
///
/// It is a singleton on purpose. Two answers from one process would be two hosts, and a match
/// would be reclaimed by the process that had just renewed it.
/// </summary>
public interface IMatchHost
{
    string HostId { get; }
}

/// <summary>
/// The identity of this process, named so that a row in the database can be traced back to
/// the process that wrote it.
/// </summary>
/// <param name="hostId">A name unique to this process.</param>
public sealed class ProcessMatchHost : IMatchHost
{
    public ProcessMatchHost() : this($"ninja-{Guid.NewGuid():N}") { }

    public ProcessMatchHost(string hostId) =>
        HostId = string.IsNullOrWhiteSpace(hostId) ? $"ninja-{Guid.NewGuid():N}" : hostId;

    public string HostId { get; }
}
