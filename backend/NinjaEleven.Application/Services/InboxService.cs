using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using NinjaEleven.Application.Abstractions;
using NinjaEleven.Application.Models;
using NinjaEleven.Application.Repositories;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InboxService> _logger;

    public InboxService(
        IInboxMessageRepository messages,
        ITeamRepository teams,
        IUnitOfWork unitOfWork,
        ILogger<InboxService> logger)
    {
        _messages = messages;
        _teams = teams;
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
    /// Reports a line of the club's book as news.
    ///
    /// Every movement the engine writes comes through here, whatever wrote it: the gate, the
    /// wage bill, a sponsor, a signing, a prize. The message is composed from the line itself
    /// and from the club's name, so a writer in another service cannot report the money
    /// wrongly — the numbers in the message are the numbers in the book, by construction.
    ///
    /// The two lines that state a balance rather than moving one are not news. "Capital
    /// inicial" and the balance carried from last season are the club's starting position, not
    /// something that happened to it, and a box that reported them would open the season with
    /// two messages about money the club already had.
    /// </summary>
    public async Task PostFinanceAsync(
        FinanceMovement line,
        IEnumerable<InboxPersonDto>? mentions = null,
        CancellationToken cancellationToken = default)
    {
        if (FinanceMovement.StatesABalance(line.Kind))
        {
            return;
        }

        var club = await _teams.GetAsync(line.TeamId, cancellationToken);
        if (club is null || !club.IsManagerClub)
        {
            return;
        }

        var amount = Math.Abs(line.Amount);
        var money = Limo(amount);
        var balance = Limo(line.BalanceAfter);
        var day = line.MatchDayNumber is { } number ? $"no dia {number}" : "fora de um dia de jogo";

        var subject = line.Amount >= 0m
            ? $"{club.Name} em alta: {money} entram no caixa {day}"
            : $"{club.Name} em baixa: {money} saem do caixa {day}";

        var headline = line.Amount >= 0m
            ? $"O caixa do {club.Name} ganhou {money}. A entrada veio de \"{line.Description}\" e deixou a Tesouraria com {balance} em mãos."
            : $"O caixa do {club.Name} perdeu {money}. A saída foi \"{line.Description}\" e deixou a Tesouraria com {balance} em mãos.";

        var body = new StringBuilder()
            .AppendLine(headline)
            .AppendLine()
            .Append(CommentOn(line.Kind, money, amount));

        await DeliverAsync(
            InboxMessage.Create(
                line.TeamId,
                InboxCategory.Finance,
                subject,
                $"Tesouraria do {club.Name}",
                body.ToString(),
                // The line's own id: a line is written once, so the message that reports it
                // can only be written once, whatever tries to write it a second time.
                reference: $"finance:{line.Id}",
                linkLabel: "Ver o extrato",
                linkRoute: "/financeiro",
                mentions: ToMentions(mentions)),
            cancellationToken);
    }

    /// <summary>
    /// The sentence under the headline of a money message, said the way a page says it.
    ///
    /// It is one sentence, and it exists because a subject line of "Salário -120.000" is a
    /// number and a number is not a story. What a manager wants to know about a movement is
    /// what kind of thing it is — a receipt that arrives every matchday, a cost decided once,
    /// a prize nobody can ask to be deferred — and that is what the kind of the line knows.
    /// </summary>
    private static string CommentOn(FinanceMovementKind kind, string money, decimal amount) => kind switch
    {
        FinanceMovementKind.GateRevenue =>
            "A receita do estádio é repartida entre os dois clubes: o mandante fica com a maior parte e o visitante com o que sobrou. É a mesma entrada em todo jogo, e o tamanho dela é o tamanho do público.",
        FinanceMovementKind.Wages =>
            "A folha é paga uma vez por rodada do campeonato, e é a maior despesa fixa do ano. O que sai hoje é uma das fatias iguais da conta da temporada.",
        FinanceMovementKind.Sponsorship =>
            "O patrocínio é a receita que não depende de o time jogar bem: vem na data, vai para o caixa e continua enquanto o contrato estiver vigente.",
        FinanceMovementKind.TransferOut =>
            "Contratação é a única despesa do clube que se anuncia antes de acontecer — e a única que a torcida só aprova depois de ver o jogador jogar.",
        FinanceMovementKind.TransferIn =>
            "Venda é o avesso da contratação: o caixa recebe hoje e asportiva se cobra amanhã, com o mesmo nome em campo ou não.",
        FinanceMovementKind.PrizeMoney =>
            "Prêmio é o dinheiro que a competição paga, e é a única entrada do caixa que ninguém pode pedir para ser adiada.",
        _ => $"O lançamento entrou no livro como \"{money}\", e o saldo do clube é o que a linha deixa."
    };

    /// <summary>
    /// Writes up how a match ended, for the club the manager is running.
    ///
    /// The facts arrive resolved (see <see cref="MatchReportFacts"/>) because the service that
    /// settled the match is the one that knows them. What is written here is the account: the
    /// headline a page would print, the goals in the sentences the match announced them with,
    /// the eleven in the order the pitch had them, the cards, and the next commitment.
    ///
    /// The next opponent is in the message because a result on its own is half a story: what
    /// a manager does with the news of a defeat is read the next fixture, and a message that
    /// stopped at the whistle would make him go and look.
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
        body.AppendLine($"Gols: {(facts.GoalLines.Count > 0 ? string.Join(" ", facts.GoalLines) : "ninguém achou a rede.")}");
        body.AppendLine();
        body.AppendLine(LineupParagraph(facts));

        if (facts.Substitutes.Count > 0)
        {
            body.AppendLine();
            body.Append($"Entraram: {List(facts.Substitutes.Select(person => person.Name))}.");
        }

        body.AppendLine();
        body.AppendLine(CardsParagraph(facts));

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
    /// The subject of a result, in the words a sports desk would use for it.
    ///
    /// It is the whole message on a narrow column, so it carries the score, both clubs and
    /// the size of the result — a line that only said "Fim de jogo" is a line a manager has
    /// to open to find out whether to be pleased.
    /// </summary>
    private static string ReportSubject(MatchReportFacts facts)
    {
        var margin = facts.ClubGoals - facts.OpponentGoals;
        var score = $"{facts.ClubGoals} x {facts.OpponentGoals}";
        var club = facts.ClubName;
        var opponent = facts.OpponentName;

        return (margin, facts.ClubGoals, facts.OpponentGoals) switch
        {
            (>= 3, _, _) => $"Goleada do {club}: {score} e o {opponent} não teveChance",
            (2, _, _) => $"Vitória por {score}: o {club} vira a chave contra o {opponent}",
            (1, _, _) => $"Sucesso do {club} por {score} sobre o {opponent}",
            (0, 0, 0) => $"Nada de gols: {club} e {opponent} empatam em {score}",
            (0, _, _) => $"Empate por {score}: {club} e {opponent} não saem do zero",
            (_, 0, _) => $"Derrota do {club}: o {opponent} vence por {score} sem sofrer gol",
            _ => $"Derrota por {score}: o {opponent} vira o jogo contra o {club}"
        };
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

        return $"{facts.ClubName} {verb} {facts.OpponentName} por {facts.ClubGoals} x {facts.OpponentGoals} {where} — {facts.PhaseName} da {facts.CompetitionName}{leg}{day}.";
    }

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
    /// The cards, and the men who took them.
    ///
    /// A red is called out on its own because it is not a yellow that was worse: it is a man
    /// who will not be there next week, and a manager reading this at breakfast needs to
    /// know that before he reads anything else.
    /// </summary>
    private static string CardsParagraph(MatchReportFacts facts)
    {
        if (facts.Booked.Count == 0)
        {
            return "Nenhum cartão no confronto.";
        }

        var names = List(facts.Booked.Select(person => person.Name));
        return $"Cartões: {names}.";
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
            .AppendLine($"{from} ofereceu {fee} pelo {facts.PlayerName}. A proposta está na mesa e o prazo do mercado continua correndo.")
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
