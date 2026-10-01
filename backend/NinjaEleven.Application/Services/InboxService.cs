using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Finance;
using NinjaEleven.Domain.Inbox;

namespace NinjaEleven.Application.Services;

/// <summary>
/// The manager's inbox: the box he reads, and the only thing in the game that writes to it.
///
/// **It is written by the engine and never by the manager.** There is no endpoint that takes
/// a message and no field a user can fill, and that is the whole reason the feature is safe
/// to be this opinionated about the prose. Every word in a message is written by a piece of
/// the world that has already decided what happened, so the box is a second voice of the
/// game rather than a place the player types into.
///
/// **Only a club somebody is running is written to.** The rule lives here, in one place, and
/// every writer comes through it: a club with no manager has nobody to deliver to, and thirty
/// five of them would otherwise be told their own gate receipts in a box nobody ever opens.
///
/// **Every message is written once.** The reference is the guard and it is the same one the
/// ledger keeps, so a match settled twice, a prize paid twice, or a process that was down
/// over a weekend and came back does not send the same news twice. A box that repeats itself
/// is a box a manager learns to distrust, which is worse than a box that is quiet.
///
/// **The money is the same money the game says everywhere else.** The Limo is written here
/// from the same rule as the client's <c>formatLimo</c>, because a message that quoted
/// "120,000" beside a ledger that says "L$ 120.000" would be a message in another language.
/// </summary>
public class InboxService
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

    private const int MaxPageSize = 50;

    private readonly IInboxMessageRepository _messages;
    private readonly ITeamRepository _teams;
    private readonly IManagedClubReader _managedClubs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InboxService> _logger;

    public InboxService(
        IInboxMessageRepository messages,
        ITeamRepository teams,
        IManagedClubReader managedClubs,
        IUnitOfWork unitOfWork,
        ILogger<InboxService> logger)
    {
        _messages = messages;
        _teams = teams;
        _managedClubs = managedClubs;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// A page of a club's box, newest first, with the number of unread messages beside it.
    ///
    /// A page past the end of the book is answered with the last page rather than with
    /// nothing: a manager who has scrolled to the bottom of a season's messages and then
    /// deleted the filter he was reading under should land on the end of what he asked for.
    /// </summary>
    public async Task<InboxBox> GetBoxAsync(
        Guid teamId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var sizeOfPage = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = await _messages.CountAsync(teamId, cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)sizeOfPage));
        var wanted = Math.Clamp(page, 1, totalPages);

        var lines = await _messages.ListAsync(
            teamId,
            skip: (wanted - 1) * sizeOfPage,
            take: sizeOfPage,
            cancellationToken);

        return new InboxBox
        {
            Messages = lines.Select(ToLine).ToList(),
            Page = wanted,
            PageSize = sizeOfPage,
            TotalItems = total,
            TotalPages = totalPages,
            UnreadCount = await _messages.UnreadCountAsync(teamId, cancellationToken)
        };
    }

    /// <summary>How many messages the manager has not opened. The number on the column.</summary>
    public async Task<int> GetUnreadCountAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await _messages.UnreadCountAsync(teamId, cancellationToken);

    /// <summary>
    /// Opens a message.
    ///
    /// The message is read by the club, not by whoever happens to hold the id: a manager who
    /// guesses his way to somebody else's line is told the line is not his, which is the only
    /// honest answer and the same one the rest of the game gives.
    /// </summary>
    public async Task<InboxMessageLine?> MarkReadAsync(
        Guid teamId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _messages.GetAsync(messageId, cancellationToken);

        if (message is null || message.RecipientTeamId != teamId)
        {
            return null;
        }

        // Reopening a message does not move the moment it was read: "read at" is when the
        // manager read it, and a second visit is not a second reading.
        message.MarkRead();
        _messages.Update(message);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToLine(message);
    }

    /// <summary>Empties the unread badge in one go, for a manager who has caught up.</summary>
    public async Task<int> MarkAllReadAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        // Only the page of unread lines is written rather than a set-based update over the
        // whole book: a manager's box is a few hundred lines, a read flag is the only thing
        // being written, and an update statement that bypasses the domain would write a
        // message's "read at" as nothing at all.
        var unread = await _messages.ListAsync(teamId, skip: 0, take: MaxPageSize, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var marked = 0;

        foreach (var message in unread.Where(message => !message.IsRead))
        {
            message.MarkRead(now);
            _messages.Update(message);
            marked++;
        }

        if (marked > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return marked;
    }

    // ---------------------------------------------------------------- writing

    /// <summary>
    /// Reports a competition's money as news.
    ///
    /// <para>
    /// A prize is the only line of a club's book that is worth a message of its own, and the
    /// reason is in <see cref="FinanceMovement.IsWorthAMessage"/>: a gate receipt and a slice
    /// of the wage bill arrive every matchday and are already on the statement, while a purse
    /// arrives twice a season and nowhere else. The message says what the prize was for and
    /// what it did to the balance, and it is composed from the line itself so the number in
    /// the message is the number in the book.
    /// </para>
    /// </summary>
    public async Task PostPrizeAsync(
        FinanceMovement line,
        CancellationToken cancellationToken = default)
    {
        var club = await _teams.GetAsync(line.TeamId, cancellationToken);
        if (club is null || !club.IsManagerClub)
        {
            return;
        }

        var money = Limo(Math.Abs(line.Amount));
        var balance = Limo(line.BalanceAfter);

        var body = new StringBuilder()
            .AppendLine($"O {club.Name} recebeu {money} da competição: \"{line.Description}\". O prêmio entrou no caixa e a Tesouraria ficou com {balance}.")
            .AppendLine()
            .Append(CommentOnThePrize(line.Description, money));

        await DeliverAsync(
            InboxMessage.Create(
                line.TeamId,
                InboxCategory.Finance,
                $"Prêmio para o {club.Name}: {money} — \"{line.Description}\"",
                $"Tesouraria do {club.Name}",
                body.ToString(),
                // The line's own reference is what makes it once: the same prize asked about
                // twice — a season closed again, a cup tie settled twice — is one purse.
                reference: $"prize:{line.Reference ?? line.Id.ToString()}",
                linkLabel: "Ver o extrato",
                linkRoute: "/financeiro",
                mentions: ToMentions(null)),
            cancellationToken);
    }

    /// <summary>
    /// The sentence under a prize, said the way a desk says it.
    ///
    /// It reads the description rather than branching on a kind, because the three prizes a
    /// club can win in a season are told apart by what they are for — a place in a table, a
    /// trophy, a run of goals — and the description is the one place that already knows.
    /// </summary>
    private static string CommentOnThePrize(string description, string money)
    {
        if (description.Contains("Artilharia", StringComparison.OrdinalIgnoreCase))
        {
            var what = description.Split('—')[0].Trim();
            return $"A artilharia se paga ao artilheiro e vai para o clube junto com ele: foi {money} que o {what} ganhou na competição.";
        }

        return description.Contains("copa", StringComparison.OrdinalIgnoreCase)
            ? "A copa paga uma vez por campanha e paga o vice também, porque chegar à final já custou uma temporada inteira."
            : "A premiação da divisão é paga a todas as posições, e é o preço de terminar a tabela onde ela terminou.";
    }

    /// <summary>
    /// Writes up how a match ended, for the club the manager is running.
    ///
    /// <para>
    /// It is written as a sports page rather than as a match receipt, and the difference is
    /// what it leads with. A receipt says "venceu por 3 x 1" and stops; a page says what kind
    /// of 3 x 1 it was — a turnaround, a collapse, a whitewash, a goalless afternoon — and
    /// then says what it did to the season, because a result in isolation is a fact about
    /// ninety minutes and a manager reads a box to find out about the other thirty rounds.
    /// </para>
    ///
    /// <para>
    /// The facts arrive resolved (see <see cref="MatchReportFacts"/>) because the service that
    /// settled the match is the one that knows them: the shape of the result, the two
    /// positions in the table, the goals with their minutes, the men who will be missing. What
    /// is written here is the account, and the account cannot disagree with the facts because
    /// it is made of them.
    /// </para>
    /// </summary>
    public async Task PostMatchReportAsync(
        MatchReportFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken))
        {
            return;
        }

        var mentions = new List<InboxPersonDto>
        {
            Mention(facts.ClubId, facts.ClubName, InboxMentionKind.Team),
            Mention(facts.OpponentId, facts.OpponentName, InboxMentionKind.Team)
        };

        if (facts.NextOpponentId is { } nextId && facts.NextOpponentName is { Length: > 0 } nextName)
        {
            mentions.Add(Mention(nextId, nextName, InboxMentionKind.Team));
        }

        mentions.AddRange(facts.Lineup);
        mentions.AddRange(facts.Substitutes);
        mentions.AddRange(facts.GoalScorers);
        mentions.AddRange(facts.Booked);

        var body = new StringBuilder();

        body.AppendLine(ReportHeadline(facts));
        body.AppendLine();

        var table = TableParagraph(facts);
        if (table is not null)
        {
            body.AppendLine(table);
            body.AppendLine();
        }

        var cup = CupParagraph(facts);
        if (cup is not null)
        {
            body.AppendLine(cup);
            body.AppendLine();
        }

        body.AppendLine(GoalsParagraph(facts));

        var absences = AbsenceParagraph(facts);
        if (absences is not null)
        {
            body.AppendLine();
            body.AppendLine(absences);
        }

        var cards = CardsParagraph(facts);
        if (cards is not null)
        {
            body.AppendLine();
            body.AppendLine(cards);
        }

        body.AppendLine();
        body.AppendLine(LineupParagraph(facts));

        if (facts.Substitutes.Count > 0)
        {
            body.AppendLine();
            body.Append($"Entraram: {List(facts.Substitutes.Select(person => person.Name))}.");
        }

        var next = NextParagraph(facts);
        if (next is not null)
        {
            body.AppendLine();
            body.Append(next);
        }

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.MatchReport,
                ReportSubject(facts),
                "Redação do Ninja Eleven",
                body.ToString(),
                reference: $"match:{facts.MatchId}:result",
                linkLabel: "Ver o resultado da partida",
                linkRoute: $"/match/{facts.MatchId}",
                mentions: ToMentions(mentions)),
            cancellationToken);
    }

    /// <summary>
    /// The subject, in the words a sports desk would put above the fold.
    ///
    /// <para>
    /// It carries the shape of the result before it carries the score, because the score is
    /// the part every subject can say and the shape is the part that makes a manager open it:
    /// a 3 x 1 and a 3 x 1 where the club was two down at half time are the same two numbers
    /// and two entirely different evenings.
    /// </para>
    ///
    /// <para>
    /// The table move is in the subject when there is one, because "subiu três posições" is
    /// the half of the news a manager cannot work out from the score.
    /// </para>
    /// </summary>
    private static string ReportSubject(MatchReportFacts facts)
    {
        var club = facts.ClubName;
        var opponent = facts.OpponentName;
        var score = $"{facts.ClubGoals} x {facts.OpponentGoals}";
        var margin = facts.ClubGoals - facts.OpponentGoals;

        // The move is appended to every one of them, and not only to the ordinary ones. A
        // turnaround that leaves the club where it was is a different piece of news from a
        // turnaround that lifts it two places, and the whole difference is one clause.
        var lead = facts.Shape switch
        {
            MatchShape.Comeback =>
                $"Virada do {club}: saiu atrás e venceu o {opponent} por {score}",

            MatchShape.GreatComeback =>
                $"Virada histórica do {club}: dois ou mais gols de desvantagem e {score}",

            MatchShape.Rescued =>
                $"O {opponent} abre o jogo e o {club} empata em {score} e salva um ponto",

            MatchShape.Collapse =>
                $"O {club} entrega jogo e perde por {score} para o {opponent}",

            MatchShape.Rout =>
                $"Estava ganhando e deixou ir: o {club} perde por {score}",

            MatchShape.Squandered =>
                $"O {club} tinha dois ou mais gols na mão e empatou em {score}",

            MatchShape.Whitewash when margin > 0 =>
                $"Goleada do {club}: {score} e o {opponent} não teve chance",

            MatchShape.Whitewash =>
                $"O {opponent} goleia o {club} por {score}",

            MatchShape.Goalless =>
                $"Nada de gols: {club} e {opponent} empatam em {score}",

            MatchShape.BalancedDraw when facts.ClubGoals > 0 =>
                $"Empate por {score} entre {club} e {opponent}",

            MatchShape.GoalFest =>
                $"Noite de gols e empate {score} entre {club} e {opponent}",

            _ when margin > 0 => $"Vitória do {club} por {score} sobre o {opponent}",

            _ when margin < 0 => $"Derrota do {club}: o {opponent} vence por {score}",

            _ => $"Empate por {score}: {club} e {opponent} não saem do zero"
        };

        return lead + MovementClause(facts.Competition);
    }

    /// <summary>
    /// Where the result left the club, told as a movement rather than as a position.
    ///
    /// It is null for a competition with no table, and for a table the club did not move on:
    /// a report that says "seventh, unchanged" every week is noise that trains a manager to
    /// stop reading the line that matters.
    /// </summary>
    private static string? TableParagraph(MatchReportFacts facts)
    {
        var context = facts.Competition;
        if (context is not { HasTable: true })
        {
            return null;
        }

        var parts = new List<string>();

        if (context.PositionBefore is { } before && context.PositionAfter is { } after)
        {
            parts.Add(before == after
                ? $"O {facts.ClubName} continua em {Ordinal(after)} lugar, com {context.Points} pontos."
                : after < before
                    ? $"A vitória levou o {facts.ClubName} de {Ordinal(before)} para {Ordinal(after)} lugar na tabela — {before - after} posição{(before - after == 1 ? string.Empty : "ões")} a mais, com {context.Points} pontos."
                    : $"A derrota derrubou o {facts.ClubName} de {Ordinal(before)} para {Ordinal(after)} lugar na tabela, agora com {context.Points} pontos.");
        }

        if (context.PointsToPromotion is { } toPromotion)
        {
            parts.Add($"Faltam {toPromotion} {(toPromotion == 1 ? "ponto" : "pontos")} para a zona de classificação.");
        }

        if (context.PointsToRelegation is { } toRelegation)
        {
            parts.Add($"A zona de rebaixamento está a {toRelegation} {(toRelegation == 1 ? "ponto" : "pontos")} — a distância que uma vitória costuma pagar.");
        }

        if (parts.Count == 0)
        {
            return null;
        }

        var rounds = context.RoundsRemaining is { } left
            ? left == 0
                ? " É a última rodada."
                : left == 1
                    ? " Falta uma rodada."
                    : $" Faltam {left} rodadas."
            : string.Empty;

        return string.Join(" ", parts) + rounds;
    }

    /// <summary>
    /// The knockout, told the way a cup is decided: by the tie and not by the ninety minutes.
    ///
    /// A single-leg tie that decided the champion has no next round to prepare for, so it is
    /// the whole story. A two-legged tie that is not finished says so, because a manager
    /// reading "classificado" over a 1 x 0 away would be planning for a game that is still to
    /// be played.
    /// </summary>
    private static string? CupParagraph(MatchReportFacts facts)
    {
        if (facts.Competition is not { HasTable: false } context)
        {
            return null;
        }

        return context.Advanced switch
        {
            true => $"A {facts.CompetitionName} é do {facts.ClubName}: a vaga é conquistada e a campanha continua.",
            false => $"O {facts.ClubName} está fora da {facts.CompetitionName}. É o fim da campanha.",
            null => null
        };
    }

    /// <summary>
    /// The goals, quoted in the words the match itself used.
    ///
    /// <para>
    /// The narration is read out of the match's own events rather than written again here,
    /// and that is the whole reason this paragraph is not a sentence about the goals. A match
    /// that said "GOL! João da Silva 0" is a match that said it, and a report that reworded
    /// the same afternoon would be the one place in the game where the same fixture could be
    /// told two ways. The minute and the man are in the narration already, because the engine
    /// put them there when the goal happened.
    /// </para>
    ///
    /// <para>
    /// A replayed match whose events carry no description has no narration to quote, so the
    /// structured goals are read out directly instead. That is a fallback and not a second
    /// voice: it only ever runs when the first one has nothing to say.
    /// </para>
    /// </summary>
    private static string GoalsParagraph(MatchReportFacts facts)
    {
        if (facts.ClubGoals == 0 && facts.OpponentGoals == 0)
        {
            return "Os gols da partida: ninguém achou a rede nos noventa minutos. Um ponto que valeu por um ponto, e um ponto é pouco.";
        }

        var narrated = facts.GoalLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

        if (narrated.Count > 0)
        {
            return $"Os gols da partida: {string.Join(" | ", narrated)}.";
        }

        if (facts.Goals.Count == 0)
        {
            return $"Os gols da partida: {facts.ClubGoals} x {facts.OpponentGoals}.";
        }

        var clauses = facts.Goals.Select(goal =>
        {
            var minute = $"aos {goal.Minute}'";
            var against = goal.TeamId != facts.ClubId ? " contra" : string.Empty;

            return goal.Kind switch
            {
                GoalKind.Penalty => $"{goal.PlayerName}{against} {minute}, de pênalti",
                GoalKind.OwnGoal => $"{minute}, contra, na errada de {goal.PlayerName}",
                GoalKind.Rebound => $"{goal.PlayerName}{against} {minute}, na segunda jogada",
                _ => $"{goal.PlayerName}{against} {minute}"
            };
        });

        return $"Os gols da partida: {string.Join("; ", clauses)}.";
    }

    /// <summary>
    /// The men who will be missing next week, and the reason in the order a manager cares
    /// about them.
    ///
    /// A suspension is said first because it is the one he can do nothing about and a
    /// replacement does not fix. A knock is said as bad news rather than as a fact, because
    /// it is bad news and a manager who has just been told his centre back is out for three
    /// weeks needs it said plainly rather than filed.
    /// </summary>
    private static string? AbsenceParagraph(MatchReportFacts facts)
    {
        if (facts.Absence is not { Players.Count: > 0 } absence)
        {
            return null;
        }

        var names = facts.Lineup.Concat(facts.Substitutes)
            .ToDictionary(person => person.Id, person => person.Name);

        var sentenceOn = absence.Players
            .Where(player => !names.TryGetValue(player.PlayerId, out var name) || !string.IsNullOrWhiteSpace(name))
            .Select(player =>
            {
                var name = names.TryGetValue(player.PlayerId, out var found) && !string.IsNullOrWhiteSpace(found)
                    ? found
                    : "um jogador";

                return player.Cause switch
                {
                    AbsenceCause.RedCard => $"{name} ({player.Matches} jogos por expulsão)",
                    AbsenceCause.AccumulatedYellows => $"{name} ({player.Matches} jogos por três amarelos)",
                    _ => $"{name} ({DescribeKnock(player.Matches)})"
                };
            })
            .ToList();

        if (sentenceOn.Count == 0)
        {
            return null;
        }

        var anyKnock = absence.Players.Any(player => player.Cause == AbsenceCause.Injury);

        return anyKnock
            ? $"Fora da próxima: {List(sentenceOn)}."
            : $"Pendurados para a próxima: {List(sentenceOn)}.";
    }

    /// <summary>How long a knock keeps a man out, in the words a manager would use for it.</summary>
    private static string DescribeKnock(int matches) => matches > 0
        ? $"{matches} {(matches == 1 ? "jogo" : "jogos")} por lesão"
        : "lesão";

    /// <summary>The cards, and the men who took them — or null when there were none.</summary>
    private static string? CardsParagraph(MatchReportFacts facts)
    {
        if (facts.Booked.Count == 0)
        {
            return "Nenhum cartão no confronto.";
        }

        return $"Cartões: {List(facts.Booked.Select(person => person.Name))}.";
    }

    /// <summary>
    /// The first paragraph of a report: the result, the ground and the competition.
    ///
    /// The competition and the phase are in the sentence because "venceu por 2 x 0" means
    /// three different things depending on which of them it was: a matchday of the
    /// championship, a return leg of a tie, or the final.
    /// </summary>
    private static string ReportHeadline(MatchReportFacts facts)
    {
        var margin = facts.ClubGoals - facts.OpponentGoals;
        var verb = margin switch
        {
            > 0 => "venceu o",
            < 0 => "perdeu para o",
            _ => "empatou com o"
        };

        var where = facts.IsHome ? "em casa" : "fora de casa";
        var leg = string.IsNullOrWhiteSpace(facts.LegLabel) ? string.Empty : $", {facts.LegLabel}";
        var day = facts.MatchDayNumber is { } number ? $", dia {number}" : string.Empty;
        var opening = OpeningOf(facts);

        return $"{opening}{facts.ClubName} {verb} {facts.OpponentName} por {facts.ClubGoals} x {facts.OpponentGoals} {where} — {facts.PhaseName} da {facts.CompetitionName}{leg}{day}.";
    }

    /// <summary>
    /// The two words a sports desk opens a piece with, when the match earned them.
    ///
    /// "Que jogo" for a turnaround, because the phrase is the sound a manager makes; a
    /// whitewash gets its own word rather than borrowing one, because "que jogo" in front of
    /// a 4 x 0 conceded would be saying the wrong thing enthusiastically.
    /// </summary>
    private static string OpeningOf(MatchReportFacts facts) => facts.Shape switch
    {
        MatchShape.Comeback => "Que jogo: ",
        MatchShape.GreatComeback => "Que jogo: ",
        MatchShape.Rescued => "Que jogo: ",
        MatchShape.Whitewash when facts.ClubGoals > facts.OpponentGoals => "Golada: ",
        MatchShape.Whitewash => "Sofrida: ",
        MatchShape.Collapse => "Derruba: ",
        MatchShape.Rout => "Derruba: ",
        _ => string.Empty
    };

    /// <summary>
    /// The eleven, in the order the pitch had them, and the shape they made of it.
    ///
    /// The order is the team's and not the alphabet's: a report that listed them by first
    /// name would say nothing about the team that played, and the shape is in the sentence
    /// because 4-3-3 and 4-4-2 are two different matches even when the eleven is the same.
    /// </summary>
    private static string LineupParagraph(MatchReportFacts facts)
    {
        var shape = string.IsNullOrWhiteSpace(facts.ClubFormation)
            ? string.Empty
            : $" ({facts.ClubFormation})";

        var names = List(facts.Lineup.Select(person => person.Name));

        return facts.Lineup.Count > 0
            ? $"Escalação do {facts.ClubName}{shape}: {names}."
            : $"O {facts.ClubName} não tem linha de jogo registrada para esta partida.";
    }

    /// <summary>
    /// Where the club plays next, or nothing when the season has nothing left to play.
    ///
    /// The ground is in the sentence because an away match is a different eleven to pick, and
    /// a manager choosing on Monday would rather be told the name of the pitch than find out
    /// on Saturday.
    /// </summary>
    private static string? NextParagraph(MatchReportFacts facts)
    {
        if (facts.NextFixtureId is null || string.IsNullOrWhiteSpace(facts.NextOpponentName))
        {
            return "Não há mais nenhum compromisso marcado no calendário por enquanto.";
        }

        var verb = facts.NextIsHome ? "recebe" : "visita";
        var day = facts.NextMatchDayNumber is { } number ? $" no dia {number}" : string.Empty;
        var competition = string.IsNullOrWhiteSpace(facts.NextCompetitionName)
            ? string.Empty
            : $", na {facts.NextCompetitionName}";
        var ground = string.IsNullOrWhiteSpace(facts.NextStadiumName)
            ? string.Empty
            : $", no {facts.NextStadiumName}";

        return $"No próximo compromisso o {facts.ClubName} {verb} o {facts.NextOpponentName}{day}{competition}{ground}.";
    }

    /// <summary>Ordinal in Portuguese, the way a table is read: 1º, 2º, 13º.</summary>
    private static string Ordinal(int position) => $"{position}º";

    /// <summary>
    /// "uma posição", "três posições".
    ///
    /// <para>
    /// The plural is a whole word rather than a suffix on "posição", because appending "ões"
    /// to a word already ending in "ção" produces "posiçãoões" — and it is a suffix here
    /// because the line says "{count} posição a mais", where the count is the subject and
    /// therefore has to agree with the noun that follows it.
    /// </para>
    /// </summary>
    private static string Places(int count) => count == 1
        ? "uma posição"
        : $"{count} posições";

    /// <summary>
    /// The half of the subject that is about the season rather than about the match.
    ///
    /// It is here and not in the table paragraph because a move of three places is the single
    /// most useful thing on a subject line, and a subject line is the only line of the message
    /// a manager is certain to read.
    /// </summary>
    private static string MovementClause(CompetitionContext? context)
    {
        if (context is not { HasTable: true } || context.PositionChange == 0)
        {
            return string.Empty;
        }

        var places = Places(Math.Abs(context.PositionChange));
        return context.PositionChange > 0
            ? $" e sobe {places} na tabela"
            : $" e cai {places} na tabela";
    }

    /// <summary>
    /// Tells the manager somebody wants one of his players.
    ///
    /// The asking price is in the second paragraph when there is one, and it is there next to
    /// the offer rather than somewhere in the market screen: the only question a manager has
    /// when a bid arrives is whether to take it, and that question is the difference between
    /// the two numbers.
    /// </summary>
    public async Task PostTransferOfferAsync(
        TransferOfferFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken))
        {
            return;
        }

        var fee = Limo(facts.Fee);
        var from = string.IsNullOrWhiteSpace(facts.BiddingClubName)
            ? "Um clube"
            : $"O {facts.BiddingClubName}";

        var mentions = new List<InboxPersonDto>
        {
            Mention(facts.RecipientTeamId, facts.ClubName, InboxMentionKind.Team),
            Mention(facts.PlayerId, facts.PlayerName, InboxMentionKind.Player)
        };

        if (facts.BiddingClubId is { } biddingId && !string.IsNullOrWhiteSpace(facts.BiddingClubName))
        {
            mentions.Add(Mention(biddingId, facts.BiddingClubName!, InboxMentionKind.Team));
        }

        var body = new StringBuilder()
            .AppendLine($"{from} ofereceu {fee} pelo {facts.PlayerName}. " + (
                facts.AnswerByRound is { } round
                    ? $"A proposta está na mesa e precisa de resposta até a rodada {Ordinal(round)} do campeonato."
                    : "A proposta está na mesa e fica parada até o clube responder — não há prazo correndo contra ela."))
            .AppendLine();

        if (facts.AskingPrice is { } bookValue)
        {
            var gap = facts.Fee - bookValue;
            var difference = gap switch
            {
                > 0 => $"A proposta está {Limo(gap)} acima do valor do livro.",
                < 0 => $"A proposta fica {Limo(-gap)} abaixo do valor do livro.",
                _ => "A proposta é exatamente o que o livro pedia."
            };

            body.Append(
                $"O {facts.PlayerName} está valendo {Limo(bookValue)} no livro do {facts.ClubName} e {difference}");
        }
        else
        {
            body.Append(
                $"O {facts.PlayerName} não tem valor registrado no livro do {facts.ClubName}, e a proposta é o que o mercado ofereceu por ele.");
        }

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.TransferOffer,
                $"Proposta por {facts.PlayerName}: {facts.BiddingClubName ?? "um clube do mercado"} oferece {fee}",
                $"Diretoria de Futebol do {facts.ClubName}",
                body.ToString(),
                reference: facts.Reference ?? $"offer:{facts.PlayerId}:{facts.BiddingClubId}",
                linkLabel: "Ver o mercado",
                linkRoute: "/transfer",
                mentions: ToMentions(mentions)),
            cancellationToken);
    }

    /// <summary>
    /// Tells a manager that one of his players has moved to a club in his own division.
    ///
    /// <para>
    /// Same division and not any division, because that is the only version of this a manager
    /// can do anything about. A rival he will meet next month signing his striker is a squad
    /// fact and a scouting question; a club in the fourth division taking his player is
    /// arithmetic, and the box does not need to be a newspaper.
    /// </para>
    ///
    /// <para>
    /// The message is written after the move rather than offered before it, and it says what
    /// the move was worth. A manager who did not sell is not owed an offer — the offer was
    /// somebody else's, and it was not addressed to him — but he is owed to know that the
    /// forward he has been planning around is now scoring against him in the same division.
    /// </para>
    /// </summary>
    public async Task PostPlayerDepartureAsync(
        PlayerDepartureFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken))
        {
            return;
        }

        var mentions = new List<InboxPersonDto>
        {
            Mention(facts.RecipientTeamId, facts.ClubName, InboxMentionKind.Team),
            Mention(facts.PlayerId, facts.PlayerName, InboxMentionKind.Player)
        };

        if (facts.BuyingTeamId is { } buyingId && !string.IsNullOrWhiteSpace(facts.BuyingClubName))
        {
            mentions.Add(Mention(buyingId, facts.BuyingClubName!, InboxMentionKind.Team));
        }

        // The division is named by the rule that names divisions rather than by an ordinal built here,
        // because an ordinal suffix glued to a feminine noun reads as "2ºª Divisão" — and a
        // manager who sees that once stops believing the rest of the paragraph.
        var body = new StringBuilder()
            .AppendLine(
                $"O {facts.BuyingClubName ?? "mercado"} fechou a contratação de {facts.PlayerName} " +
                $"na {CompetitionRules.DivisionName(facts.DivisionTier)}, a mesma em que o {facts.ClubName} " +
                $"joga. O jogador já consta no novo elenco e a vaga no {facts.ClubName} está aberta " +
                $"para reposição.")
            .AppendLine()
            // The fee and nothing else. A book value beside it would be a number worked from
            // the season he has just left, and the manager's question is not what his own
            // department thought of the player — it is what a rival actually paid for him, which
            // is the number that says how much of a problem the gap in his squad is going to be.
            .Append(
                facts.Fee > 0m
                    ? $"Um rival pagou {Limo(facts.Fee)} por ele, e esse é o valor que a reposição vai custar."
                    : $"A contratação foi sem custo para o {facts.BuyingClubName ?? "mercado"}, e um rival que paga nada por um homem seu não é um rival que você escolheu.");

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.TransferOffer,
                $"{facts.PlayerName} foi para o {facts.BuyingClubName ?? "mercado"}",
                $"Diretoria de Futebol do {facts.ClubName}",
                body.ToString(),
                // The deal and the player, not the season: a window can be closed twice, and a
                // window closed twice is one departure told about twice.
                reference: facts.Reference ?? $"departure:{facts.TransferId}",
                linkLabel: "Ver o mercado",
                linkRoute: "/transfer",
                mentions: ToMentions(mentions)),
            cancellationToken);
    }

    /// <summary>
    /// Announces a settled cup round to every manager in the country.
    ///
    /// <para>
    /// The one message here with no addressee of its own. A cup round is the country's
    /// football: sixty-four clubs in one bracket, and a manager whose club is not in today's
    /// ties still watched the round because the round of sixteen is where the season's biggest
    /// clubs start falling out, and a manager planning his own summer needs to know which of
    /// them are gone. Telling only the club that pressed the button would make the news depend
    /// on who happened to be walking the world.
    /// </para>
    ///
    /// <para>
    /// The tie is written with the aggregate beside it when it went to two legs, because a
    /// second leg's score is not the tie: "1 x 0, eliminado no agregado" is the fact, and a
    /// manager reading "1 x 0" alone would take it for a win.
    /// </para>
    /// </summary>
    public async Task PostCupRoundAsync(CupRoundFacts facts, CancellationToken cancellationToken = default)
    {
        if (facts.Ties.Count == 0)
        {
            return;
        }

        var recipients = await TheManagersOfTheWorldAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var body = new StringBuilder()
            .AppendLine(
                $"A {CompetitionRules.TieRoundName(facts.RoundNumber)} da {facts.CupName} foi decidida " +
                $"em {facts.SeasonName}. São {facts.Ties.Count} {(facts.Ties.Count == 1 ? "confronto" : "confrontos")}:")
            .AppendLine();

        foreach (var tie in facts.Ties)
        {
            body.AppendLine($"• {LineOfTheTie(tie)}");
        }

        var through = facts.Ties.Count(tie => tie.WinnerTeamId is not null);
        var eliminated = facts.Ties.Count - through;

        body.Append(
            eliminated == 0
                ? $"A {CompetitionRules.TieRoundName(facts.RoundNumber)} deixou {through} " +
                  $"{(through == 1 ? "clube classificado" : "clubes classificados")} e ninguém eliminado."
                : $"A {CompetitionRules.TieRoundName(facts.RoundNumber)} deixou {through} " +
                  $"{(through == 1 ? "classificado" : "classificados")}" +
                  $" e eliminou {eliminated} {(eliminated == 1 ? "clube" : "clubes")}.");

        var subject = $"{CompetitionRules.TieRoundName(facts.RoundNumber)}: " +
            (facts.Ties.Count == 1
                ? LineOfTheTie(facts.Ties[0])
                : $"{facts.Ties.Count} confrontos decididos");

        var text = body.ToString().TrimEnd();

        await WriteToEveryManagerAsync(
            recipients,
            teamId => InboxMessage.Create(
                teamId,
                InboxCategory.CupRound,
                subject,
                "Comissão Central da Competição",
                text,
                reference: facts.Reference,
                linkLabel: "Ver a Copa",
                linkRoute: "/copa",
                mentions: ToMentions(MentionsOfTheTies(facts.Ties))),
            cancellationToken);
    }

    /// <summary>
    /// One tie as a line of the round's news: the two clubs, the score, and what the score did.
    /// </summary>
    private static string LineOfTheTie(CupRoundTieFacts tie)
    {
        var score = $"{tie.HomeClubName} {tie.HomeGoals} x {tie.AwayGoals} {tie.AwayClubName}";

        if (tie.WinnerTeamId is not { } winner)
        {
            return $"{score} — a ser decidida.";
        }

        var winnerName = winner == tie.HomeTeamId ? tie.HomeClubName : tie.AwayClubName;

        if (tie.WentToPenalties)
        {
            return $"{score} — {winnerName} classificado nos pênaltis.";
        }

        // A second leg is only half the story, and saying so is the difference between a
        // manager reading a result and a manager reading a tie.
        return tie.IsSecondLeg
            ? $"{score} — {winnerName} classificado no agregado."
            : $"{score} — {winnerName} classificado.";
    }

    /// <summary>The clubs the round's news mentions, named once each.</summary>
    private static List<InboxPersonDto> MentionsOfTheTies(IReadOnlyList<CupRoundTieFacts> ties)
    {
        var mentions = new List<InboxPersonDto>();
        var named = new HashSet<Guid>();

        foreach (var tie in ties)
        {
            foreach (var club in new[] { (tie.HomeTeamId, tie.HomeClubName), (tie.AwayTeamId, tie.AwayClubName) })
            {
                if (named.Add(club.Item1))
                {
                    mentions.Add(Mention(club.Item1, club.Item2, InboxMentionKind.Team));
                }
            }
        }

        return mentions;
    }

    /// <summary>
    /// Announces a season that is over: the four final tables and who moved between them.
    ///
    /// <para>
    /// It is one message and not four, and it is sent to every manager for the same reason the
    /// cup round is: a season ends by rearranging the country. A manager in the 4ª Divisão
    /// still watched two clubs leave his table and two arrive, and four messages — one per
    /// division, each about somebody else's table — would be a season that ended four times
    /// over and never all at once.
    /// </para>
    ///
    /// <para>
    /// It is long, and it is long on purpose: this is the one message a manager can read in
    /// January and answer "what happened while I was away". The tables are the season's
    /// football and the movement is the season's consequence, and a summary that left either
    /// out would be a headline with nothing under it.
    /// </para>
    /// </summary>
    public async Task PostSeasonSummaryAsync(
        SeasonSummaryFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (facts.Divisions.Count == 0)
        {
            return;
        }

        var recipients = await TheManagersOfTheWorldAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var body = new StringBuilder()
            .AppendLine($"{facts.SeasonName} acabou. Estas foram as quatro tabelas e o que sobrou delas:")
            .AppendLine();

        foreach (var division in facts.Divisions)
        {
            var champion = division.Lines.FirstOrDefault(line => line.Position == 1);
            var relegated = division.Lines.Count(line => line.Movement == SeasonMovementKind.Relegated);
            var promoted = division.Lines.Count(line => line.Movement == SeasonMovementKind.Promoted);

            body.AppendLine(
                $"🏆 {division.DivisionName} — campeão: {champion?.ClubName ?? "—"}" +
                (promoted > 0 ? $". Subiram: {NamesOf(division, SeasonMovementKind.Promoted)}." : ".") +
                (relegated > 0 ? $" Caíram: {NamesOf(division, SeasonMovementKind.Relegated)}." : string.Empty));
        }

        body.AppendLine();

        // The names are read once for the whole fan-out, because a message that says "o
        // <club> terminou em 4º" cannot work it out from a list of tables it is holding.
        var clubs = (await _teams.ListByIdsAsync(
                recipients.ToList(), cancellationToken))
            .ToDictionary(club => club.Id, club => club.Name);

        // The four tables in full, tier by tier. It is a long message and it is long on
        // purpose: this is the one letter a manager can open in January and answer "what
        // happened while I was away", and a summary that carried only the champions and the
        // promoted would leave the question of where his own club finished unanswered — which is
        // the only part of it he cannot look up anywhere else.
        foreach (var division in facts.Divisions)
        {
            body.AppendLine($"{division.DivisionName}:");
            body.AppendLine(string.Join(
                Environment.NewLine,
                division.Lines.Select(line =>
                    $"{line.Position}º {line.ClubName} — {line.Points} pts ({line.Played}J, {line.Wins}V {line.Draws}E {line.Losses}D, {line.GoalsFor}-{line.GoalsAgainst}){MovementNote(line)}")));
            body.AppendLine();
        }

        await WriteToEveryManagerAsync(
            recipients,
            teamId =>
            {
                var text = new StringBuilder()
                    .AppendLine($"Para {NameOf(clubs, teamId)}:")
                    .AppendLine()
                    .Append(PlaceOfThisClub(facts, teamId))
                    .AppendLine()
                    .AppendLine()
                    .Append(body.ToString().TrimEnd())
                    .ToString();

                return InboxMessage.Create(
                    teamId,
                    InboxCategory.SeasonSummary,
                    $"{facts.SeasonName}: as quatro tabelas finais e o que mudou",
                    "Comissão Central da Competição",
                    text,
                    reference: facts.Reference,
                    linkLabel: "Ver o campeonato",
                    linkRoute: "/league",
                    mentions: []);
            },
            cancellationToken);
    }

    /// <summary>What became of one club: the table it was in, the place it held and where it goes.</summary>
    private static string PlaceOfThisClub(SeasonSummaryFacts facts, Guid teamId)
    {
        foreach (var division in facts.Divisions)
        {
            var line = division.Lines.FirstOrDefault(row => row.TeamId == teamId);
            if (line is null)
            {
                continue;
            }

            var movement = line.Movement switch
            {
                SeasonMovementKind.Promoted => " e sobe de divisão.",
                SeasonMovementKind.Relegated => " e cai de divisão.",
                _ => " e fica onde está."
            };

            return $"O {line.ClubName} terminou em {line.Position}º na {division.DivisionName}, " +
                   $"com {line.Points} pontos em {line.Played} jogos e saldo de {line.GoalsFor - line.GoalsAgainst}{movement}";
        }

        // A club that was in no division of the season that just closed was not in the
        // championship, and saying so is more use to its manager than a paragraph about tables
        // that never included it.
        return $"O {NameOf(null, teamId)} não esteve em nenhuma das quatro divisões nesta temporada.";
    }

    /// <summary>How a line of the table ended, in the words the movement is announced in.</summary>
    private static string MovementNote(SeasonSummaryLine line) => line.Movement switch
    {
        SeasonMovementKind.Promoted => " ↑ sobe",
        SeasonMovementKind.Relegated => " ↓ cai",
        _ => string.Empty
    };

    /// <summary>A club's name, or its id when the club is not one the world named.</summary>
    private static string NameOf(IReadOnlyDictionary<Guid, string>? clubs, Guid teamId) =>
        clubs is not null && clubs.TryGetValue(teamId, out var name) ? name : teamId.ToString();

    /// <summary>The clubs of a division that moved one way, in the order the table left them.</summary>
    private static string NamesOf(SeasonSummaryDivision division, SeasonMovementKind movement) =>
        string.Join(
            ", ",
            division.Lines
                .Where(line => line.Movement == movement)
                .OrderBy(line => line.Position)
                .Select(line => line.ClubName));

    /// <summary>
    /// The clubs that have a person behind them: the readers of a message with no addressee.
    ///
    /// <para>
    /// It is asked of the world and not of the request, so a scheduler walking the cup at three
    /// in the morning tells the same managers a hand pressing the button would. A world of
    /// nobody returns an empty list, and an empty list writes nothing at all — which is why a
    /// cup round settled in a world without managers costs no messages and no queries.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<Guid>> TheManagersOfTheWorldAsync(
        CancellationToken cancellationToken)
    {
        var managed = await _managedClubs.ListManagedClubsAsync(cancellationToken);
        if (managed.Count == 0)
        {
            return managed;
        }

        // The reader answers with the clubs somebody is running; the box still asks of each one
        // whether it may be written to, because that is the rule every writer goes through and
        // a second door into it would be a rule that could drift from the first.
        var writable = new List<Guid>();

        foreach (var teamId in managed)
        {
            if (await DeliverableAsync(teamId, cancellationToken))
            {
                writable.Add(teamId);
            }
        }

        return writable;
    }

    /// <summary>
    /// Writes one message into several managers' boxes.
    ///
    /// <para>
    /// The recipient is stamped per club on the way out, because a message row belongs to one
    /// club: the box is a club's, the unread count is a club's, and a row addressed to nobody
    /// would be a row that no box could ever list.
    /// </para>
    /// </summary>
    private async Task WriteToEveryManagerAsync(
        IReadOnlyList<Guid> recipients,
        Func<Guid, InboxMessage> messageFor,
        CancellationToken cancellationToken)
    {
        var written = 0;

        foreach (var teamId in recipients)
        {
            await DeliverAsync(messageFor(teamId), cancellationToken);
            written++;
        }

        _logger.LogInformation("Wrote a message to {Count} manager(s).", written);
    }

    /// <summary>
    /// Announces a title, and the three are announced differently.
    ///
    /// A championship is the season and says so; a cup is a run and says what it beat; an
    /// artilharia is one man's afternoon and says whose it was. The money is in the last
    /// paragraph of each because it is the last thing in the world: a manager reads the
    /// headline, reads the news, and reads the cheque.
    /// </summary>
    public async Task PostTitleAsync(TitleFacts facts, CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken) ||
            string.IsNullOrWhiteSpace(facts.Reference))
        {
            return;
        }

        var mentions = new List<InboxPersonDto>
        {
            Mention(facts.RecipientTeamId, facts.ClubName, InboxMentionKind.Team)
        };

        if (facts.PlayerId is { } playerId && !string.IsNullOrWhiteSpace(facts.PlayerName))
        {
            mentions.Add(Mention(playerId, facts.PlayerName!, InboxMentionKind.Player));
        }

        var season = string.IsNullOrWhiteSpace(facts.SeasonName) ? string.Empty : $" na {facts.SeasonName}";
        var prize = facts.PrizeMoney is > 0
            ? $" A premiação de {Limo(facts.PrizeMoney.Value)} já está no caixa do clube."
            : string.Empty;
        var goals = facts.PlayerGoals is 1 or -1 ? "gol" : "gols";

        var (subject, body) = facts.Kind switch
        {
            InboxTitleKind.Championship => (
                $"{facts.ClubName} é campeão da {facts.CompetitionName}",
                new StringBuilder()
                    .AppendLine($"O {facts.ClubName} fechou a {facts.CompetitionName}{season} em primeiro lugar. O título é do clube: a mesma equipe que começou a temporada termina a frente de todos, e o que separa um campeão dos outros é a rodada em que resolve chegar.")
                    .AppendLine()
                    .Append("A taça vai para a galeria, o nome vai para a fachada e o resto da temporada continua.")
                    .Append(prize)
                    .ToString()),
            InboxTitleKind.Cup => (
                $"{facts.ClubName} conquista a {facts.CompetitionName}",
                new StringBuilder()
                    .AppendLine($"O {facts.ClubName} levantou a {facts.CompetitionName}{season}. A taça é do clube, e a campanha até ela foi feita jogo a jogo, fase por fase, sem uma única folga.")
                    .AppendLine()
                    .Append($"A Copa Ninja Eleven não perdoa: cada fase é um adversário preparado para barrar quem chegar, e o {facts.ClubName} passou por todos eles.")
                    .Append(prize)
                    .ToString()),
            _ => ScorerAnnouncement(facts, season, goals, prize)
        };

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.Title,
                subject,
                "Comissão Organizadora do Ninja Eleven",
                body.ToString(),
                reference: $"title:{facts.Reference}",
                linkLabel: facts.LinkLabel
                    ?? (facts.Kind == InboxTitleKind.Cup ? "Ver a Copa" : "Ver o campeonato"),
                linkRoute: facts.LinkRoute
                    ?? (facts.Kind == InboxTitleKind.Cup ? "/copa" : "/league"),
                mentions: ToMentions(mentions)),
            cancellationToken);
    }

    /// <summary>
    /// Announces a shirt deal that has run out.
    ///
    /// It is worth a message of its own precisely because nothing else would say it: the money
    /// for the last match is already in the ledger and the contract simply stops coming back, so
    /// a club that did not read this would go on playing with a sponsor it is no longer being
    /// paid by. The screen in Estádio is where a renewal is signed, and the message points at it.
    /// </summary>
    public async Task PostSponsorExpiryAsync(
        SponsorExpiryFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken))
        {
            return;
        }

        var fee = Limo(facts.PerMatchFee);
        var played = facts.ContractMatches;

        var body = new StringBuilder()
            .AppendLine($"O contrato do {facts.SponsorName} com o {facts.ClubName} chegou ao fim depois de {played} jogos. A camisa volta a ser do clube a partir da próxima partida.")
            .AppendLine()
            .AppendLine($"A renda de {fee} por jogo que o patrocinador pagava deixa de entrar no caixa. Um contrato novo não é luxo: é o dinheiro que paga a folha da semana seguinte.")
            .AppendLine()
            .Append($"Enquanto o {facts.ClubName} não assinar com outro patrocinador, cada partida é jogada sem nome no peito e sem a entrada correspondente.");

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.Club,
                $"O contrato do {facts.SponsorName} com o {facts.ClubName} termina após {played} jogos",
                $"Marketing do {facts.ClubName}",
                body.ToString(),
                reference: $"sponsor:{facts.ContractId}:expired",
                linkLabel: "Ver os patrocinadores",
                linkRoute: "/estadio",
                mentions: ToMentions(
                [
                    Mention(facts.RecipientTeamId, facts.ClubName, InboxMentionKind.Team)
                ])),
            cancellationToken);
    }

    /// <summary>
    /// Writes the club's week: everything that came in, everything that went out, and what
    /// is left.
    ///
    /// <para>
    /// This is the replacement for the message-per-line the box used to send, and the shape
    /// of it is the argument. A manager does not want to be told about the gate of Tuesday,
    /// the wages of Tuesday, the sponsor's instalment of Tuesday and the training fee of
    /// Wednesday in four separate messages; he wants to be told, once a week, what the week
    /// cost and what it was worth. Four messages is four chances to not open the fifth one
    /// that matters, and the four that arrived every matchday are the four a manager learns
    /// to swipe past without reading.
    /// </para>
    ///
    /// <para>
    /// The buckets are the four the manager already thinks in — bilheteria, compras, vendas
    /// e salários — because a statement in somebody else's categories is a statement nobody
    /// reconciles against the ledger they are trying to understand. Anything that is none of
    /// those four goes in an "outros" line rather than being dropped: a training fee the
    /// manager paid is money he spent, and a statement that silently omitted it would not add
    /// up to the balance printed at the bottom.
    /// </para>
    /// </summary>
    public async Task PostStatementAsync(
        StatementFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken))
        {
            return;
        }

        var income = facts.GateRevenue + facts.Signings + facts.Sales + facts.OtherIncome;
        var expenses = facts.Wages + facts.Training + facts.OtherExpenses;
        var difference = income - expenses;

        var body = new StringBuilder()
            .AppendLine($"Resumo dos dias {facts.FromMatchDay} a {facts.ToMatchDay}: {Limo(income)} entraram e {Limo(expenses)} saíram, uma diferença de {Limo(Math.Abs(difference))} {(difference >= 0 ? "a favor" : "contra")} do {facts.ClubName}.")
            .AppendLine()
            .AppendLine(StatementLines(facts))
            .AppendLine()
            .AppendLine($"O caixa começou a semana com {Limo(facts.OpeningBalance)} e fecha com {Limo(facts.ClosingBalance)}.");

        if (difference < 0m)
        {
            body.AppendLine();
            body.Append("Semana de despesa acima da receita: é a folha fazendo o que faz, mas um clube que repete essa conta fecha o ano no vermelho.");
        }
        else if (difference > 0m)
        {
            body.AppendLine();
            body.Append("A receita segurou a despesa da semana, e é o que permite planejar a próxima.");
        }

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.Finance,
                $"Extrato da semana ({facts.FromMatchDay}–{facts.ToMatchDay}) — " +
                $"{(difference >= 0 ? "sobrou" : "faltou")} {Limo(Math.Abs(difference))}",
                $"Tesouraria do {facts.ClubName}",
                body.ToString(),
                reference: facts.Reference,
                linkLabel: "Ver o extrato",
                linkRoute: "/financeiro",
                mentions: ToMentions(null)),
            cancellationToken);
    }

    /// <summary>
    /// The week's own lines, one per bucket, and only the buckets that moved.
    ///
    /// A line of zero is not information: a club with no sales that week does not need to be
    /// told it did not sell anybody, and four empty lines make the two real ones harder to
    /// find on a screen a manager reads on a phone.
    /// </summary>
    private static string StatementLines(StatementFacts facts)
    {
        var entries = new (string Label, decimal Amount, string Verb)[]
        {
            (facts.HomeMatches > 0
                ? $"Bilheteria ({facts.HomeMatches} {(facts.HomeMatches == 1 ? "jogo em casa" : "jogos em casa")})"
                : "Bilheteria",
                facts.GateRevenue, "Entrou"),
            ("Vendas de jogadores", facts.Sales, "Entrou"),
            ("Compras de jogadores", facts.Signings, "Saiu"),
            ("Salários", facts.Wages, "Saiu"),
            ("Treinos", facts.Training, "Saiu")
        };

        var written = new List<string>();

        foreach (var (label, amount, verb) in entries)
        {
            if (amount == 0m)
            {
                continue;
            }

            written.Add(verb == "Entrou"
                ? $"{label}: {Limo(amount)} entraram"
                : $"{label}: {Limo(amount)} saíram");
        }

        if (facts.OtherIncome != 0m)
        {
            written.Add($"Outras entradas: {Limo(facts.OtherIncome)} entraram");
        }

        if (facts.OtherExpenses != 0m)
        {
            written.Add($"Outras despesas: {Limo(facts.OtherExpenses)} saíram");
        }

        return written.Count == 0
            ? "Nenhuma entrada nem saída no período."
            : string.Join("\n", written);
    }

    /// <summary>
    /// Announces a shirt deal somebody has just signed.
    ///
    /// <para>
    /// It is the counterpart of the expiry and it is worth a message for the same reason: the
    /// money only arrives a match at a time, so a deal that was signed and never announced is
    /// a deal the manager finds out about from the statement seven days later, with a name on
    /// his shirt he cannot place.
    /// </para>
    /// </summary>
    public async Task PostSponsorSignedAsync(
        SponsorSignedFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken))
        {
            return;
        }

        var fee = Limo(facts.PerMatchFee);
        var played = facts.ContractMatches;

        var body = new StringBuilder()
            .AppendLine($"O {facts.ClubName} fechou com o {facts.SponsorName}: são {fee} por jogo, por {played} jogos.")
            .AppendLine()
            .AppendLine($"São {Limo(facts.PerMatchFee * played)} no contrato inteiro, e cada partida paga a sua parte na hora em que é jogada — não adiantado, não no fim do mês: junto com a bilheteria.")
            .AppendLine()
            .Append($"O nome do {facts.SponsorName} entra na camisa a partir da próxima partida em casa. Se o clube for eliminado ou rebaixado antes de {played} jogos, o que sobrou do contrato é perdido.");

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.Club,
                $"{facts.ClubName} assina com o {facts.SponsorName}: {fee} por jogo",
                $"Marketing do {facts.ClubName}",
                body.ToString(),
                reference: $"sponsor:{facts.ContractId}:signed",
                linkLabel: "Ver os patrocinadores",
                linkRoute: "/estadio",
                mentions: ToMentions(
                [
                    Mention(facts.RecipientTeamId, facts.ClubName, InboxMentionKind.Team)
                ])),
            cancellationToken);
    }

    /// <summary>
    /// Warns a club that a man is in the last year of his deal, once, on the season boundary.
    ///
    /// <para>
    /// It is one message per man and not one per squad, because the decision is taken one man at
    /// a time: a manager renews a striker or lets him go and does both to a winger in the same
    /// breath, and a single message listing five expiring contracts would be a list to be read
    /// rather than a set of decisions to be made.
    /// </para>
    ///
    /// <para>
    /// It carries both wages on purpose. What the club is paying is the cost of doing nothing
    /// and what he would be paid is the cost of signing him again, and a manager asked to renew
    /// a man without the second number is being asked without the question. Where the two are
    /// equal the sentence says so rather than letting him work out that nothing has changed.
    /// </para>
    /// </summary>
    public async Task PostContractExpiringAsync(
        ContractExpiringFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken))
        {
            return;
        }

        var now = Limo(facts.Wage);
        var again = Limo(facts.WageOnRenewal);
        var same = facts.Wage == facts.WageOnRenewal;

        var body = new StringBuilder()
            .AppendLine($"O contrato de {facts.PlayerName} chega ao fim ao fim desta temporada — é a última dele no {facts.ClubName}.")
            .AppendLine()
            .AppendLine(same
                ? $"O clube paga {now} por temporada por ele, e é o que pagaria se renovasse: o valor não mudou desde a assinatura, e só muda quando alguém decide que mudou."
                : $"O clube paga {now} por temporada por ele hoje. Se o contrato for renovado agora, o valor passa para {again}.")

            .AppendLine()
            .AppendLine("Depois desta temporada ele fica livre: qualquer clube podeLEVá-lo sem pagar nada ao clube, e o clube não pode impedir. A decisão é agora — renovar por quantas temporadas o clube quiser, entre uma e cinco.");

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.Club,
                $"{facts.PlayerName} está no último ano de contrato",
                $"Departamento de futebol — {facts.ClubName}",
                body.ToString(),
                reference: $"contract:{facts.ContractId}:expiring",
                linkLabel: "Ver o elenco",
                linkRoute: "/elenco",
                mentions: ToMentions(
                [
                    Mention(facts.RecipientTeamId, facts.ClubName, InboxMentionKind.Team),
                    Mention(facts.PlayerId, facts.PlayerName, InboxMentionKind.Player)
                ])),
            cancellationToken);
    }

    /// <summary>
    /// Answers a manager's bid, in the words the answer actually arrived in.
    ///
    /// The three refusals are kept apart because they are three different things a manager can
    /// act on, and only one of them is about the price. A club that kept the man on a roll is
    /// a club that was offered too little for *him*; a club that would not sell at any price is
    /// a club whose squad is at the bound and would be left without a side; and a club that
    /// never answered is a club whose desk did not get to the letter in time. Collapsing them
    /// would leave a manager who was outbid thinking he had been outplayed, which is a different
    /// mistake and a different next bid.
    /// </summary>
    public async Task PostTransferDecisionAsync(
        TransferDecisionFacts facts,
        CancellationToken cancellationToken = default)
    {
        if (!await DeliverableAsync(facts.RecipientTeamId, cancellationToken))
        {
            return;
        }

        var fee = Limo(facts.Fee);
        var other = $"O {facts.OtherClubName}";

        var mentions = new List<InboxPersonDto>
        {
            Mention(facts.RecipientTeamId, facts.ClubName, InboxMentionKind.Team),
            Mention(facts.PlayerId, facts.PlayerName, InboxMentionKind.Player),
            Mention(facts.OtherClubId, facts.OtherClubName, InboxMentionKind.Team)
        };

        var (subject, lead, coda) = Decision(facts, fee, other);

        var body = new StringBuilder()
            .AppendLine(lead)
            .AppendLine()
            .Append(coda);

        await DeliverAsync(
            InboxMessage.Create(
                facts.RecipientTeamId,
                InboxCategory.TransferOffer,
                subject,
                facts.ManagerIsSeller
                    ? $"Diretoria de Futebol do {facts.OtherClubName}"
                    : $"Diretoria de Futebol do {facts.ClubName}",
                body.ToString(),
                reference: $"{facts.Reference}:{SlugOf(facts.Outcome)}",
                linkLabel: "Ver o mercado",
                linkRoute: "/transfer",
                mentions: ToMentions(mentions)),
            cancellationToken);
    }

    /// <summary>
    /// The three sentences of a decision: the headline, the news, and what it means now.
    ///
    /// A manager reads the subject on a narrow column and the first line of the body, so the
    /// subject has to carry the outcome on its own — "o mercado responde" is a subject that
    /// makes him open the message to find out whether to be pleased, and that is a click a
    /// column of forty lines cannot afford.
    /// </summary>
    private static (string Subject, string Lead, string Coda) Decision(
        TransferDecisionFacts facts,
        string fee,
        string other)
    {
        var player = facts.PlayerName;
        var club = facts.ClubName;
        var otherClub = facts.OtherClubName;
        var wasSeller = facts.ManagerIsSeller;

        return facts.Outcome switch
        {
            InboxDecision.Accepted when wasSeller => (
                $"{otherClub} compra {player} do {club} por {fee}",
                $"{other} fechou a compra de {player} por {fee}. O acordo é do mercado e a proposta é do {club}: foi o valor que o vendedor pediu e o comprador pagou.",
                $"A proposta estava na mesa do {club} e foi aceita. A saída é de {club} e a entrada é do comprador, e a partir de agora o nome do {player} no elenco é uma pergunta de quando, não de se."),

            InboxDecision.Accepted => (
                $"{otherClub} aceita proposta do {club} por {player}: {fee}",
                $"{other} aceitou a proposta do {club} por {player} por {fee}. O preço foi aceito e o negócio está fechado: falta a janela para a transferência se concretizar.",
                $"A proposta do {club} era por {player} e saiu aceita por {fee}. O preço já está combinado, e a única coisa que falta é a janela abrir para a transferência se concretizar."),

            InboxDecision.RefusedOnPrice => (
                $"{otherClub} recusa proposta do {club}: {player} está caro demais",
                $"{other} recusou a proposta do {club} por {player}. A recusa foi no preço: a proposta ficou abaixo do que o clube pediu por ele, e nenhuma roleta foi jogada sobre isso.",
                $"O valor recusado foi de {fee}. Uma recusa no preço é a única que se resolve com um número maior na proposta seguinte — as outras dependem de quanto o clube quer o jogador, e não de quanto se oferece por ele."),

            InboxDecision.RefusedOnThePlayer => (
                $"{otherClub} recusa proposta do {club} por {player}",
                $"{other} recusou a proposta do {club} por {player}, a {fee}. O preço estava dentro do que o clube pede, e ainda assim a venda não saiu: o valor do jogador foi o que pesou, não a proposta.",
                $"Recusada por decisão do clube, e não por falta de предложa. Um clube que recusa o preço certo está dizendo que o {player} vale mais do que os números dizem — e o que muda a resposta é o que o mercado oferecer por ele na próxima."),

            InboxDecision.RefusedOnTheSquad => (
                $"{otherClub} recusa proposta do {club} por {player}: elenco no limite",
                $"{other} recusou a proposta do {club} por {player}, a {fee}. O clube está no número mínimo de jogadores e não pode vender mais ninguém sem ficar sem um time para jogar.",
                $"A recusa não foi sobre o {player} nem sobre o preço: foi sobre o tamanho do elenco. Enquanto {other} estiver no limite, ele recusa qualquer proposta por qualquer um dos seus jogadores, por maior que seja o valor."),

            InboxDecision.Expired => wasSeller
                ? (
                    $"Proposta do {otherClub} por {player} caduca no mercado",
                    $"{other} proposeu {player} ao {club} por {fee}, e a proposta chegou ao fim do prazo sem resposta. O mercado parou de esperar: a proposta saiu da mesa e o {player} está livre para receber uma nova oferta de qualquer clube.",
                    $"O que fica é o {player} de volta à lista, e o mercado voltando a olhar para ele na próxima rodada. Uma proposta que caduca não é uma recusa: é uma mesa que não respondeu a tempo, e a próxima oferta é feita de novo, do zero.")
                : (
                    $"Proposta do {club} por {player} expira sem resposta",
                    $"A proposta do {club} por {player}, de {fee}, chegou ao fim do prazo e expirou sem resposta. {other} não decidiu, e o mercado parou de esperar: a proposta não está mais na mesa e o {player} está livre para qualquer clube que quiser fazer uma nova.",
                    $"Uma proposta que caduca não é uma recusa: é a mesa do {other} que não chegou à carta a tempo. O {player} está de volta ao mercado, e a próxima proposta começa do zero."),

            _ => (
                $"Acordo por {player} entre {club} e {otherClub} é cancelado",
                $"O acordo por {player} entre {club} e {otherClub}, acertado por {fee}, foi cancelado antes de se concretizar. O dinheiro que tinha sido combinado voltou para quem o pagou, e a transferência não acontece.",
                $"O acordo existiu e foi desfeito: um negócio cancelado não é a mesma coisa que uma proposta recusada, e a diferença é que ninguém escolheu recusar. O que o trava é a folha do clube no momento da janela — o elenco chegou ao limite, ou o jogador deixou de estar nele.")
        };
    }

    /// <summary>
    /// The word a decision is written under, so a proposal that is accepted and then called off
    /// is two messages and not one written twice.
    /// </summary>
    private static string SlugOf(InboxDecision outcome) => outcome switch
    {
        InboxDecision.Accepted => "accepted",
        InboxDecision.CalledOff => "called-off",
        InboxDecision.Expired => "expired",
        _ => "refused"
    };

    /// <summary>
    /// The announcement of an artilharia, which is a different sentence for each of the three
    /// places.
    ///
    /// The winner's is a coronation and the other two are a table, and telling a manager his
    /// striker "é o artilheiro" when the striker finished third would be the one sentence in
    /// the box that is simply false. The three are paid together and they are read together, so
    /// each one names the place it actually took.
    /// </summary>
    private static (string Subject, string Body) ScorerAnnouncement(
        TitleFacts facts,
        string season,
        string goals,
        string prize)
    {
        var place = facts.Position ?? 1;

        var (subject, lead) = place switch
        {
            1 => (
                $"{facts.PlayerName} é o artilheiro da {facts.CompetitionName}",
                $"{facts.PlayerName} terminou a {facts.CompetitionName}{season} como o maior artilheiro, com {facts.PlayerGoals} {goals} na competição inteira — a artilharia se decide por um ponto e por um jogo, e foi neste."),
            2 => (
                $"{facts.PlayerName} fica em segundo na artilharia da {facts.CompetitionName}",
                $"{facts.PlayerName} terminou a {facts.CompetitionName}{season} com {facts.PlayerGoals} {goals} e a vice-liderança da artilharia. Segundo lugar é pago: a cadeia inteira é dividida com quem ficou em terceiro, e o que se decide na reta final é quem recebe mais."),
            _ => (
                $"{facts.PlayerName} fica em terceiro na artilharia da {facts.CompetitionName}",
                $"{facts.PlayerName} terminou a {facts.CompetitionName}{season} com {facts.PlayerGoals} {goals} e o terceiro lugar da artilharia. O pódio é pequeno e o terceiro ainda é pódio — é uma das três únicas posições que a competição paga.")
        };

        var body = new StringBuilder()
            .AppendLine(lead)
            .AppendLine()
            .Append($"O nome de {facts.PlayerName} é do {facts.ClubName}, e é o clube que leva o prêmio junto com ele.")
            .Append(prize)
            .ToString();

        return (subject, body);
    }

    // ---------------------------------------------------------------- plumbing

    /// <summary>
    /// Whether a message addressed to this club is one anybody will read.
    ///
    /// This is the rule about the computer-controlled clubs, and it is asked here rather than
    /// at each of the dozen places a message could be written: a club with no manager has
    /// nobody to deliver to, and thirty five of them being told their own gate receipts would
    /// be thirty five clubs writing rows nobody ever asks for.
    /// </summary>
    private async Task<bool> DeliverableAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var team = await _teams.GetAsync(teamId, cancellationToken);
        return team is { IsManagerClub: true };
    }

    /// <summary>
    /// Writes the message, unless it has already been written.
    ///
    /// The guard is asked before the message is built rather than after it is written,
    /// because the message is only words and the database should never see a row it is going
    /// to refuse.
    /// </summary>
    private async Task DeliverAsync(InboxMessage message, CancellationToken cancellationToken)
    {
        if (await _messages.ExistsWithReferenceAsync(
                message.RecipientTeamId, message.Reference, cancellationToken))
        {
            _logger.LogDebug(
                "Message {Reference} had already been delivered to {TeamId} and was not written again.",
                message.Reference,
                message.RecipientTeamId);

            return;
        }

        await _messages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Delivered a {Category} message to {TeamId} as {Reference}.",
            message.Category,
            message.RecipientTeamId,
            message.Reference);
    }

    private static InboxMessageLine ToLine(InboxMessage message) => new()
    {
        Id = message.Id,
        Category = message.Category,
        Subject = message.Subject,
        SenderName = message.SenderName,
        Body = message.Body,
        Mentions = InboxMentions.Parse(message.Mentions)
            .Select(mention => new InboxPersonDto
            {
                Name = mention.Name,
                Kind = mention.Kind,
                Id = mention.EntityId
            })
            .ToList(),
        LinkLabel = message.LinkLabel,
        LinkRoute = message.LinkRoute,
        CreatedAt = message.CreatedAt,
        IsRead = message.IsRead,
        ReadAt = message.ReadAt
    };

    private static InboxPersonDto Mention(Guid id, string name, string kind) => new()
    {
        Id = id,
        Name = name,
        Kind = kind
    };

    private static IEnumerable<InboxMention> ToMentions(IEnumerable<InboxPersonDto>? people) =>
        (people ?? Array.Empty<InboxPersonDto>()).Select(person => new InboxMention
        {
            Name = person.Name,
            Kind = person.Kind,
            EntityId = person.Id
        });

    /// <summary>
    /// A list of names the way a sentence writes one: "A, B e C", and "A e B" for two.
    /// </summary>
    private static string List(IEnumerable<string> names)
    {
        var written = names.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();

        return written.Count switch
        {
            0 => string.Empty,
            1 => written[0],
            _ => $"{string.Join(", ", written.Take(written.Count - 1))} e {written[^1]}"
        };
    }

    /// <summary>
    /// An amount in the game's money: `L$ 1.234.567`, or `L$ 3.943,33` when there are cents.
    ///
    /// The rule is the client's, on purpose. A message that quoted "120,000" beside a ledger
    /// saying "L$ 120.000" would be a message written in a different language from the game
    /// it belongs to, and the whole point of a stored document is that it is the same words
    /// every time it is read.
    /// </summary>
    private static string Limo(decimal value) =>
        $"L$ {value.ToString("N2", Portuguese).TrimEnd('0').TrimEnd(',')}";
}
