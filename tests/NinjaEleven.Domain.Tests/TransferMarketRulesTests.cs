using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Transfers;
using Xunit;

namespace NinjaEleven.Domain.Tests;

/// <summary>
/// The rules the market is made of: when a man may arrive, how big a book a club may keep,
/// what a release costs, when a retirement may be announced, and the order the market is read
/// in.
///
/// Every test here is a sentence a manager could say out loud. The window and the retirement
/// deadline in particular are the two the whole market hangs on, and both were once a
/// constant in a service rather than a rule with a name — which is exactly how a squad ends up
/// planned around a window that closes on a different day from the one the calendar closes it.
/// </summary>
public class TransferMarketRulesTests
{
    // ---------------------------------------------------------------- the window

    [Theory]
    [InlineData(1, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(17, false)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void A_window_is_one_of_the_two_rounds_the_rules_name(
        int round,
        bool expected)
    {
        Assert.Equal(expected, TransferWindowRules.IsOpen(round));
    }

    /// <summary>
    /// Two mid-season windows, and both of them before the Supercup: a proposal made at any
    /// point up to the twentieth round arrives in the season it was made in.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(20)]
    public void A_proposal_made_up_to_the_last_window_arrives_in_the_same_season(int round)
    {
        Assert.Equal(3, TransferWindowRules.ArrivalSeasonNumberFor(round, 3));
    }

    /// <summary>
    /// A season has three arrivals and no more: the opening, before anything has been played —
    /// the Supercup's own round — and two mid-season windows. Anything else a proposal is told
    /// to wait for is a fourth window nobody asked for, so this asserts the whole set rather
    /// than three constants that could each move on their own.
    /// </summary>
    [Fact]
    public void A_season_has_exactly_three_arrivals()
    {
        var arrivals = Enumerable.Range(1, CompetitionRules.LeagueMatchDays)
            .Select(round => TransferWindowRules.ArrivalRoundFor(round))
            .Distinct()
            .OrderBy(round => round)
            .ToList();

        Assert.Equal(
            new[]
            {
                1,
                TransferWindowRules.FirstArrivalRound,
                TransferWindowRules.SecondArrivalRound
            },
            arrivals);
    }

    /// <summary>
    /// The opening is an arrival like any other: a proposal made after the last mid-season
    /// window waits for the first round of the next season, which is the round the Supercup
    /// opens and the only arrival before any of this season's football has been played.
    /// </summary>
    [Fact]
    public void The_first_arrival_of_a_season_is_its_opening_round()
    {
        const int thisSeason = 3;

        var arrivalSeason = TransferWindowRules.ArrivalSeasonNumberFor(21, thisSeason);
        var arrivalRound = TransferWindowRules.ArrivalRoundFor(21);

        Assert.Equal(thisSeason + 1, arrivalSeason);
        Assert.Equal(1, arrivalRound);
    }

    [Fact]
    public void A_proposal_made_before_the_first_window_arrives_in_it()
    {
        // The window is still ahead of him, so he walks in when it closes.
        Assert.Equal(
            TransferWindowRules.FirstArrivalRound,
            TransferWindowRules.ArrivalRoundFor(4));
    }

    [Fact]
    public void A_proposal_made_between_the_two_windows_arrives_in_the_second()
    {
        // The first window has gone and the second has not: waiting for the later one is
        // waiting for the last arrival of the year rather than for next season's Supercup.
        Assert.Equal(
            TransferWindowRules.SecondArrivalRound,
            TransferWindowRules.ArrivalRoundFor(14));
        Assert.Equal(3, TransferWindowRules.ArrivalSeasonNumberFor(14, 3));
    }

    [Fact]
    public void A_proposal_made_after_the_last_window_arrives_in_the_next_season()
    {
        // The windows have already come and gone. The next thing that happens is the Supercup,
        // which is the first match of the next season and the arrival the rules name first.
        Assert.Equal(4, TransferWindowRules.ArrivalSeasonNumberFor(21, 3));
        Assert.Equal(1, TransferWindowRules.ArrivalRoundFor(21));
    }

    [Fact]
    public void The_arrival_a_proposal_names_does_not_move_when_the_world_moves_on()
    {
        // The whole point of deciding the arrival when the deal is made: a manager planning a
        // squad must not find the arrival moved because he looked again in a later round.
        var roundAtProposal = 9;
        var seasonAtProposal = 2;

        var arrivalSeason = TransferWindowRules.ArrivalSeasonNumberFor(roundAtProposal, seasonAtProposal);
        var arrivalRound = TransferWindowRules.ArrivalRoundFor(roundAtProposal);

        Assert.Equal(2, arrivalSeason);
        Assert.Equal(TransferWindowRules.FirstArrivalRound, arrivalRound);

        // Read again two rounds later, out of curiosity rather than intent: the second window
        // is now the one ahead of the proposal, and it is a different arrival.
        Assert.NotEqual(
            arrivalRound,
            TransferWindowRules.ArrivalRoundFor(roundAtProposal + 2));
    }

    // ---------------------------------------------------------------- the book

    [Theory]
    [InlineData(SquadSizeRules.MinSquadSize, true)]
    [InlineData(23, true)]
    [InlineData(SquadSizeRules.MaxSquadSize, false)]
    [InlineData(SquadSizeRules.MaxSquadSize + 1, false)]
    public void A_club_may_not_go_over_the_maximum(int size, bool expected)
    {
        Assert.Equal(expected, SquadSizeRules.CanAddOne(size));
    }

    [Theory]
    [InlineData(SquadSizeRules.MinSquadSize, false)]
    [InlineData(SquadSizeRules.MinSquadSize + 1, true)]
    [InlineData(23, true)]
    public void A_club_may_not_go_under_the_minimum(int size, bool expected)
    {
        Assert.Equal(expected, SquadSizeRules.CanRemoveOne(size));
    }

    [Fact]
    public void The_two_bounds_agree_with_the_validity_test()
    {
        // CanAddOne answers about the maximum and CanRemoveOne about the minimum, because a
        // club under the minimum is one the world already fixed and a club over the maximum is
        // one a signing could cause. Neither of them is asked about the other end, so this
        // checks each against the half of the range it speaks about.
        for (var size = SquadSizeRules.MinSquadSize; size <= SquadSizeRules.MaxSquadSize + 2; size++)
        {
            if (SquadSizeRules.CanAddOne(size))
            {
                Assert.True(
                    size + 1 <= SquadSizeRules.MaxSquadSize,
                    $"{size} + 1 is over the maximum, and CanAddOne allowed it.");
            }

            if (SquadSizeRules.CanRemoveOne(size))
            {
                Assert.True(
                    size - 1 >= SquadSizeRules.MinSquadSize,
                    $"{size} - 1 is under the minimum, and CanRemoveOne allowed it.");
            }
        }
    }

    // ---------------------------------------------------------------- the release

    [Fact]
    public void A_release_costs_half_of_what_the_club_still_owes()
    {
        // 1,000 per round; 8 rounds left this season and two more seasons promised after it.
        // Owed: 8,000 + (22 x 2 x 1,000) = 52,000. Half is 26,000.
        var cost = ReleaseRules.ReleaseCost(
            salaryPerRound: 1_000m,
            roundsLeftThisSeason: 8,
            seasonsLeftAfterThis: 2,
            roundsPerSeason: 22);

        Assert.Equal(26_000m, cost);
    }

    [Fact]
    public void A_release_at_the_end_of_a_contract_costs_nothing()
    {
        // The last round of the last season: there is no wage left to settle.
        Assert.Equal(
            0m,
            ReleaseRules.ReleaseCost(
                salaryPerRound: 1_000m,
                roundsLeftThisSeason: 0,
                seasonsLeftAfterThis: 0,
                roundsPerSeason: 22));
    }

    [Fact]
    public void A_release_costs_more_the_longer_the_contract_runs()
    {
        var short_ = ReleaseRules.ReleaseCost(1_000m, 10, 0, 22);
        var long_ = ReleaseRules.ReleaseCost(1_000m, 10, 3, 22);

        Assert.True(long_ > short_);
        Assert.Equal(5_000m, short_);
        Assert.Equal(38_000m, long_);
    }

    // ------------------------------------------------------------- the quote on a card

    [Fact]
    public void The_quote_on_a_card_is_the_settlement_the_release_charges()
    {
        // The same four facts the release command works from, in the shape a squad row and a
        // player's card hold them: a season's wage, how many rounds are left in the season and
        // how many seasons the contract has left counting this one.
        var quoted = ReleaseRules.QuoteReleaseCost(
            seasonWage: 22_000m,
            roundsPerSeason: 22,
            roundsLeftThisSeason: 8,
            seasonsLeftIncludingThisOne: 3);

        var charged = ReleaseRules.ReleaseCost(
            salaryPerRound: 22_000m / 22,
            roundsLeftThisSeason: 8,
            seasonsLeftAfterThis: 2,
            roundsPerSeason: 22);

        Assert.Equal(charged, quoted);
    }

    [Fact]
    public void A_man_in_his_last_season_is_quoted_only_the_rounds_that_are_left()
    {
        // One season counting the current one, so nothing is promised after it: the settlement
        // is half a season's remaining wages and no more.
        var quoted = ReleaseRules.QuoteReleaseCost(
            seasonWage: 22_000m,
            roundsPerSeason: 22,
            roundsLeftThisSeason: 8,
            seasonsLeftIncludingThisOne: 1);

        Assert.Equal(4_000m, quoted);
    }

    [Fact]
    public void A_quote_is_not_worked_out_of_a_negative_contract()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ReleaseRules.QuoteReleaseCost(1_000m, 22, 5, -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ReleaseRules.QuoteReleaseCost(-1m, 22, 5, 2));
    }

    [Theory]
    [InlineData(-1, 5, 1, 22)]
    [InlineData(1000, -1, 1, 22)]
    [InlineData(1000, 5, -1, 22)]
    [InlineData(1000, 5, 1, 0)]
    public void A_settlement_is_not_worked_out_of_nonsense(
        decimal salaryPerRound,
        int rounds,
        int seasons,
        int roundsPerSeason)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ReleaseRules.ReleaseCost(salaryPerRound, rounds, seasons, roundsPerSeason));
    }

    // ---------------------------------------------------------------- the retirement

    [Theory]
    [InlineData(36, false)]
    [InlineData(37, true)]
    [InlineData(40, true)]
    [InlineData(42, true)]
    [InlineData(43, false)]
    public void A_man_retires_between_thirty_seven_and_forty_two(int age, bool expected)
    {
        Assert.Equal(expected, RetirementRules.CanRetireAt(age));
    }

    // ---------------------------------------------------------------- calling a deal off

    [Fact]
    public void A_deal_agreed_by_both_clubs_can_be_called_off_when_it_stops_being_possible()
    {
        // The window settles deals that were signed weeks earlier, and by then the seller may
        // have let the man go, may not be able to pay, or may be one player short of a side it
        // is allowed to field. There is no longer a proposal to refuse — both clubs said yes —
        // so the deal is called off, and a domain that refused to say that would throw halfway
        // through a matchday's transfers and leave the rest of them unsettled.
        var today = new DateOnly(2026, 9, 28);
        var deal = Transfer.Propose(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            arrivalSeasonNumber: 1, arrivalSeasonId: null, fee: 1_000m, proposedAt: today);

        deal.Accept(today);
        deal.CallOff(today.AddDays(20));

        Assert.Equal(TransferStatus.Rejected, deal.Status);
    }

    [Fact]
    public void A_deal_still_waiting_for_an_answer_cannot_be_called_off()
    {
        // Calling a deal off is what the window does to a deal that was agreed. A proposal
        // nobody has answered is a different thing, and it expires on its own rule instead.
        var today = new DateOnly(2026, 9, 28);
        var proposal = Transfer.Propose(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            arrivalSeasonNumber: 1, arrivalSeasonId: null, fee: 1_000m, proposedAt: today);

        Assert.Throws<InvalidOperationException>(() => proposal.CallOff(today));
    }

    [Fact]
    public void A_selling_club_cannot_refuse_a_deal_it_has_already_accepted()
    {
        var today = new DateOnly(2026, 9, 28);
        var deal = Transfer.Propose(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            arrivalSeasonNumber: 1, arrivalSeasonId: null, fee: 1_000m, proposedAt: today);

        deal.Accept(today);

        Assert.Throws<InvalidOperationException>(() => deal.Reject(today.AddDays(1)));
    }

    // ---------------------------------------------------------------- the order
    [Fact]
    public void A_player_keeps_the_same_place_in_the_market_for_ever()
    {
        // The order is a hash, not a draw. Page two of a search is the same page two on every
        // call, and a man who fits the filters can still be found.
        var player = Guid.NewGuid();

        Assert.Equal(MarketOrder.KeyOf(player), MarketOrder.KeyOf(player));
    }

    [Fact]
    public void Two_players_are_rarely_ordered_alike()
    {
        // The property the sort depends on. Not a collision test — a birthday-paradox one, over
        // a hundred players, because a market where a fifth of the men share a place is a
        // market whose second page is the first page again.
        var players = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();
        var keys = players.Select(MarketOrder.KeyOf).ToList();

        Assert.Equal(players.Count, keys.Distinct().Count());
    }

    [Fact]
    public void The_order_spreads_across_the_range_rather_than_clumping()
    {
        // A hash that returned a small number for everything would sort into a handful of
        // buckets and the market would read as a league table. The values have to use the
        // space they are given.
        var keys = Enumerable.Range(0, 200).Select(_ => MarketOrder.KeyOf(Guid.NewGuid())).ToList();

        var buckets = keys.Select(key => (uint)key >> 24).Distinct().Count();

        Assert.True(buckets > 8, $"the order used {buckets} of 256 buckets over 200 players.");
    }
}
