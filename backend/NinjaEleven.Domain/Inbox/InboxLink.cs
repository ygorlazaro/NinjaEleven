namespace NinjaEleven.Domain.Inbox;

/// <summary>
/// The doors a message may open.
///
/// <para>
/// They are named here and written nowhere else, because a message's door is a promise: the
/// label on the button says where the manager is going and the route has to be that place. The
/// box had it the other way round in five places at once — a message labelled "Ver os
/// patrocinadores" that pointed at the stadium, and four messages whose routes
/// (<c>/elenco</c>, <c>/mercado</c>, <c>/base</c>) are not routes this game has at all, so the
/// frontend's catch-all answered them by sending the manager to the club selector. Nothing
/// looked broken: a route that does not exist does not fail, it is simply somewhere else.
/// </para>
///
/// <para>
/// So the vocabulary is small, it is written once, and a new door is a new name here rather
/// than another string in another message. The frontend's own table is the other half of the
/// promise, and the two have to agree: the club is <c>/team/{id}</c>, its base is
/// <c>/team/{id}/base</c>, the market is <c>/transfer</c> and the companies that pay for the
/// shirt are <c>/patrocinadores</c> — which is where the ground is deliberately not, because a
/// message about a deal that has run out is a message about a signature nobody has made yet.
/// </para>
/// </summary>
public static class InboxLink
{
    /// <summary>The club's ledger, where the money the message is about is written down.</summary>
    public const string Financeiro = "/financeiro";

    /// <summary>
    /// The prefix the club's own screens hang from: the squad at <c>/team/{id}</c> and the
    /// base at <c>/team/{id}/base</c>.
    ///
    /// It is private because a prefix is not a door: there is no screen at <c>/team</c>, and a
    /// vocabulary whose members are all doors cannot hold one that would send a manager
    /// nowhere.
    /// </summary>
    private const string Squad = "/team";

    /// <summary>The market, where an offer is answered and a player is listed.</summary>
    public const string Transfer = "/transfer";

    /// <summary>The championship: the table, the artilharia and the rules.</summary>
    public const string League = "/league";

    /// <summary>The knockout, and the round the message is about.</summary>
    public const string Cup = "/copa";

    /// <summary>
    /// The companies on the shirt: the shortlist, the fee each pays and the signature.
    ///
    /// It is its own screen and not a tab of the ground, so a deal is signed where the deals
    /// are and a message about one is not a message about the pitch.
    /// </summary>
    public const string Sponsors = "/patrocinadores";

    /// <summary>The club's academy, where a youth who has evolved belongs.</summary>
    public static string Academy(Guid teamId) => $"{Squad}/{teamId}/base";

    /// <summary>
    /// A match, for the report the box wrote about it.
    /// </summary>
    public static string Match(Guid matchId) => $"/match/{matchId}";

    /// <summary>
    /// The club itself, for a message whose subject is the club rather than one of its men.
    /// </summary>
    public static string Team(Guid teamId) => $"{Squad}/{teamId}";
}