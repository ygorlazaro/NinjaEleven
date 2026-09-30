namespace NinjaEleven.Domain.Competitions;

/// <summary>
/// How far a window of football has been taken by whoever is moving the world.
///
/// <para>
/// A window used to know only whether it was over (<see cref="Round.CompletedAt"/>), which is
/// enough for a season that a person advances by hand and not enough for one the Scheduler
/// advances by itself: a process that dies half way through a round has to be able to come
/// back and find out that the round is half played, and two processes that fire at the same
/// time have to be able to see that one of them already has it.
/// </para>
///
/// <para>
/// So the window carries a small machine of its own. It is deliberately separate from
/// <see cref="Round.CompletedAt"/>, which is written when the <i>last fixture</i> finishes and
/// is what pays the artilharia and the transfer market: a window can be half executed and
/// still not be over, and a window can be over without the Scheduler ever having run it — the
/// manager played it himself. Two facts, two columns.
/// </para>
/// </summary>
public enum RoundExecutionStatus
{
    /// <summary>Nobody has taken this window yet.</summary>
    Scheduled = 0,

    /// <summary>
    /// Somebody is playing it right now. The claim carries a timestamp, so a window whose
    /// owner died can be taken over by the next process to ask — see
    /// <see cref="Round.CanBeClaimed"/>.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Every fixture of the window has been played and none of them is left. This is the
    /// answer a second execution attempt is refused on.
    /// </summary>
    Completed = 2
}
