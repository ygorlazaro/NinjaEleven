using NinjaEleven.Domain.Common;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.BalanceLab;

/// <summary>
/// Whether the laboratory is measuring the same model the engine plays.
///
/// <para>
/// A balance laboratory that has drifted from production reports on a game nobody plays, and
/// every number it prints is then worse than useless — it is confidently wrong. The check is
/// the only honest thing the class does: build real players at real attributes, let the
/// engine's own <c>TeamStrength.Of</c> measure them, let <see cref="LabStrength"/> measure
/// the same cards under the engine's own ramp, and require the two to agree to the last
/// decimal.
/// </para>
/// </summary>
public static class Fidelity
{
    /// <summary>
    /// The ramp production ships, read off the engine's own two constants. It is not a
    /// hardcoded copy of them, because a hardcoded copy is a laboratory that starts drifting
    /// the next time the constants are tuned.
    /// </summary>
    public static IEnergyCurve Production => new ComposedEnergyCurve(
        MatchRules.EnergyFactorFloor,
        MatchRules.EnergyFactorGamma);

    /// <summary>
    /// Builds a snapshot the engine would build, so <c>TeamStrength.Of</c> has something real
    /// to read. The card's stamina is dropped on the way in because the entity has nowhere to
    /// put it yet — which is the honest state of things and is worth being explicit about
    /// rather than quietly ignoring.
    /// </summary>
    private static MatchPlayerSnapshot Snapshot(PlayerCard card, Guid teamId)
    {
        var player = Player.Create(
            "Fidelity",
            card.Age,
            card.Position,
            card.Speed,
            card.Accuracy,
            card.Dribbling,
            card.Heading,
            card.Strength,
            card.Position == Position.GK ? card.GoalkeeperPower : 0,
            card.Position == Position.GK ? card.Reflexes : 0);

        var state = PlayerSeasonState.Create(Guid.NewGuid(), Guid.NewGuid(), teamId, card.Energy);

        return MatchPlayerSnapshot.FromPlayerSeasonState(player, state);
    }

    /// <summary>The worst relative disagreement between the two implementations over a grid of squads.</summary>
    public static (double WorstError, string WorstCase) CheckAgainstProduction()
    {
        var random = new LabRandom(20260930);
        var worst = 0.0;
        var worstCase = string.Empty;
        var teamId = Guid.NewGuid();

        foreach (var quality in new[] { 15, 40, 70, 95 })
        {
            foreach (var energy in new[] { 10, 40, 70, 100 })
            {
                foreach (var shape in new[] { (4, 3, 3), (3, 4, 3), (4, 4, 2), (5, 3, 2) })
                {
                    var cards = new List<PlayerCard>
                    {
                        PlayerCard.Keeper(quality + 5, energy)
                    };

                    for (var index = 0; index < shape.Item1; index++)
                    {
                        cards.Add(PlayerCard.Outfield(Position.DEF, quality, energy));
                    }

                    for (var index = 0; index < shape.Item2; index++)
                    {
                        cards.Add(PlayerCard.Outfield(Position.MID, quality, energy));
                    }

                    for (var index = 0; index < shape.Item3; index++)
                    {
                        cards.Add(PlayerCard.Outfield(Position.ATT, quality, energy));
                    }

                    var production = TeamStrength.Of(cards.Select(card => Snapshot(card, teamId)).ToList());
                    var lab = LabStrength.Of(cards, Production);

                    var scale = Math.Max(
                        Math.Max(Math.Abs(production.Attack), Math.Abs(production.Midfield)),
                        Math.Abs(production.Defense));

                    if (scale < 1e-9)
                    {
                        continue;
                    }

                    var error = Math.Max(
                        Math.Max(
                            Math.Abs(production.Attack - lab.Attack),
                            Math.Abs(production.Midfield - lab.Midfield)),
                        Math.Abs(production.Defense - lab.Defense)) / scale;

                    if (error > worst)
                    {
                        worst = error;
                        worstCase = $"qualidade {quality}, energia {energy}, formato {shape.Item1}-{shape.Item2}-{shape.Item3}";
                    }
                }
            }
        }

        return (worst, worstCase);
    }

    /// <summary>
    /// Rolls a match out with the engine itself, so the duel laboratory can be checked
    /// against the only thing that actually plays football.
    ///
    /// <para>
    /// It is here as a guard on the laboratory and not as a deliverable: a duel model that
    /// disagrees with the engine about who wins would make every balance number it prints a
    /// statement about a game that is not played. The check is coarse — an average over a few
    /// hundred matches — because it is checking a direction, not a decimal.
    /// </para>
    /// </summary>
    public static (double StrongWinRate, double WeakWinRate) EngineCrossCheck(int matches)
    {
        var strongWins = 0;
        var weakWins = 0;

        for (var seed = 1; seed <= matches; seed++)
        {
            var home = new TeamInfo(Guid.NewGuid(), "Fortes", "FOR", "#FF0000", "#FFF", 80);
            var away = new TeamInfo(Guid.NewGuid(), "Fracos", "FRA", "#0000FF", "#FFF", 40);
            var homeTeamId = home.Id;
            var awayTeamId = away.Id;

            var random = new DeterministicRandomSource(seed);

            var context = new MatchContext(
                Guid.NewGuid(),
                home,
                away,
                Eleven(homeTeamId, quality: 80, energy: 100, seed),
                Eleven(awayTeamId, quality: 40, energy: 100, seed + 50_000),
                Bench(homeTeamId, 70, 100, seed + 1),
                Bench(awayTeamId, 35, 100, seed + 1),
                random);

            var state = new MatchState(context);
            var engine = new MatchEngine(random);

            engine.Initialize(state, 0).ToList();

            for (var tick = 0; tick < 600 && !state.MatchFinished; tick++)
            {
                if (state.HalfTimePauseActive)
                {
                    engine.ContinueSecondHalf(state);
                }

                engine.Tick(state).ToList();
            }

            if (state.HomeScore > state.AwayScore)
            {
                strongWins++;
            }
            else if (state.AwayScore > state.HomeScore)
            {
                weakWins++;
            }
        }

        return ((double)strongWins / matches, (double)weakWins / matches);
    }

    private static List<MatchPlayerSnapshot> Eleven(Guid teamId, int quality, int energy, int seed)
    {
        var random = new LabRandom(seed);
        var lineup = new List<MatchPlayerSnapshot>
        {
            Make(teamId, Position.GK, quality, energy, random, 0)
        };

        foreach (var position in Enumerable.Repeat(Position.DEF, 4)
                     .Concat(Enumerable.Repeat(Position.MID, 3))
                     .Concat(Enumerable.Repeat(Position.ATT, 3)))
        {
            lineup.Add(Make(teamId, position, quality, energy, random, lineup.Count));
        }

        return lineup;
    }

    private static List<MatchPlayerSnapshot> Bench(Guid teamId, int quality, int energy, int seed)
    {
        var random = new LabRandom(seed);

        return new[] { Position.GK, Position.DEF, Position.MID, Position.ATT, Position.DEF }
            .Select((position, index) => Make(teamId, position, quality, energy, random, 50 + index))
            .ToList();
    }

    private static MatchPlayerSnapshot Make(Guid teamId, Position position, int quality, int energy, LabRandom random, int index)
    {
        var jitter = (int)Math.Round((random.NextDouble() - 0.5) * quality * 0.2);
        var card = position == Position.GK
            ? PlayerCard.Keeper(Math.Clamp(quality + jitter, 1, 100), energy)
            : PlayerCard.Outfield(position, Math.Clamp(quality + jitter, 1, 100), energy);

        return Snapshot(card, teamId);
    }
}
