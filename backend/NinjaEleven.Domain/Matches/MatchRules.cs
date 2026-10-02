namespace NinjaEleven.Domain.Matches;

/// <summary>
/// Every number the match model is balanced on, in one place.
///
/// The engine used to carry its own constants and they drifted apart: one rule would be
/// tuned in the action loop while the rule it was supposed to be consistent with lived in
/// another method. Splitting them into named constants with a reason attached to each is
/// what makes the model readable — a reviewer can see the whole football of a match
/// without reading the loops that apply it.
///
/// Each group below is one of the rules the model is built on. They are all plain
/// constants on purpose: nothing here is configurable per club, and a value that changes
/// between two matches of the same season is a bug, not a setting.
/// </summary>
public static class MatchRules
{
    /// <summary>
    /// The clock. A tick is half a minute of football, not a whole one: at one tick per
    /// minute a goal, a card and a substitution all share the same instant, and the feed
    /// stops reading as a sequence of things that happened.
    /// </summary>
    public const int SecondsPerTick = 30;

    public const int FirstHalfEnd = 45;

    /// <summary>
    /// How many ticks the clock stands still after something the crowd needs to see. The
    /// hold is counted in ticks rather than in seconds of wall clock so the pause lasts the
    /// same stretch of the match at every speed, and a goal always arrives with room to
    /// be looked at.
    /// </summary>
    public const int TicksHeldAfterMajorEvent = 3;

    /// <summary>
    /// Added time. The total is drawn once at kick-off, the way a referee announces it
    /// only when he reaches the end of the half, and then split between the two halves.
    /// </summary>
    /// <summary>
    /// When the referee tells the crowd how long the first half will run.
    ///
    /// Not at the whistle and not four minutes from it, but four minutes before: 41' is
    /// where a real manager learns it, with the half still running and the crowd still
    /// arguing about it, instead of being told it is already over.
    /// </summary>
    public const int FirstHalfStoppageAnnounceMinute = 41;

    /// <summary>
    /// The same for the second half, and for the same reason.
    /// </summary>
    public const int SecondHalfStoppageAnnounceMinute = 86;

    public const int MinStoppage = 2;
    public const int MaxStoppage = 5;

    /// <summary>
    /// The share of the added time that belongs to the first half. A first half collects
    /// injuries, the second half collects goals, so the split is not 50/50.
    /// </summary>
    public const double FirstHalfStoppageShare = 0.4;

    // --- Who has the ball -------------------------------------------------------

    /// <summary>
    /// How often anything at all happens. A tick that does nothing is not a wasted tick:
    /// it is most of a football match, and it is what makes the moments land. Roughly a
    /// quarter of them is what puts a real number of chances into a real ninety minutes;
    /// any less and matches end 0-0 more often than they should.
    /// </summary>
    public const double ActionChancePerTick = 0.24;

    /// <summary>
    /// Weight of the attack in the initiative. Midfield decides who plays and attack
    /// decides what happens next, so an attack-heavy side takes more of the ball without a
    /// pure striker's eleven dominating it.
    /// </summary>
    public const double InitiativeAttackWeight = 0.65;

    /// <summary>
    /// How much of the ball each side keeps. Never 0 or 100: a side that never sees the
    /// other half is not a football match, and the number on the screen has to be read.
    /// </summary>
    public const int MinPossession = 30;
    public const int MaxPossession = 70;

    // --- The action table -------------------------------------------------------

    /// <summary>
    /// A built-up attack: the ball is carried, the defence is beaten and a shot comes
    /// from somewhere. It is the only branch that narrates more than one step, because it
    /// is the only one that has more than one step.
    /// </summary>
    public const double AttackSequenceChance = 0.30;

    /// <summary>
    /// The bands of the action table, read in order off one roll: a booking, a red card, a
    /// foul, a corner, a shot, and everything above the last is a midfield duel.
    ///
    /// These bands are the shape of a football match rather than the shape of a prototype's
    /// feed. The prototype had fouls at one action in seventeen, which produced a match
    /// with no fouls in it and a penalty once every ten matches — and a match with no fouls
    /// in it has no cards, no penalties and no eleven metres, which is most of what a
    /// football match is. Around a fifth of the actions is a foul here, which is where a
    /// real match's fouls, bookings and penalties all come from.
    /// </summary>
    public const double YellowCardLimit = 0.06;
    public const double DirectRedCardLimit = 0.075;
    public const double FoulLimit = 0.28;
    public const double CornerLimit = 0.36;
    public const double ShotLimit = 0.72;

    // --- Dueling -----------------------------------------------------------------

    /// <summary>
    /// What a carrier is worth against a marker when two equal men meet: the base both sides
    /// are measured from.
    /// </summary>
    /// <remarks>
    /// It was a bare <c>0.56</c> before, which meant a duel was decided by the same roll in
    /// every duel of every match, and a match of them was a match in which no pair of players
    /// was ever better than another pair. The base is a little under a coin because football
    /// is not a coin, and the swing below is what makes the two men in the duel matter.
    /// </remarks>
    public const double DuelBaseChance = 0.56;

    /// <summary>
    /// How much of the attribute gap between the two men in a duel reaches the roll. It is
    /// applied to a -1..1 difference (see <see cref="AttributeScale.Factor(double)"/>), so a
    /// world-class dribbler against a squad-player marker is worth about 0.40 more than a
    /// coin and the reverse pairing about 0.40 less.
    /// </summary>
    /// <remarks>
    /// It is a swing and not a coefficient on raw attributes because the raw version was
    /// already answered: <c>0.52 + (dribbling − strength) × 0.018</c> over 1..100 is
    /// <b>1.00 for every pair thirty-five points apart or more</b>, so the whole world played
    /// the same duel. Normalising first is what turns the same forty points into a difference
    /// that is still a difference at ninety.
    /// </remarks>
    public const double DuelSwing = 0.40;
    public const double MinDuelChance = 0.08;
    public const double MaxDuelChance = 0.94;

    // --- Shooting ---------------------------------------------------------------

    /// <summary>
    /// The band a shot can leave. A shot is not a coin flip, but the keeper is a real
    /// obstacle, so no combination of attributes can turn it into a certainty.
    /// </summary>
    public const double MinGoalChance = 0.12;
    public const double MaxGoalChance = 0.50;

    /// <summary>
    /// What an average shot on target is worth before the striker and the keeper are read:
    /// the base both of them are measured from.
    /// </summary>
    public const double BaseGoalChance = 0.31;

    /// <summary>
    /// How much of the -1..1 gap between the striker's shot and the keeper's hands reaches the
    /// roll.
    /// </summary>
    /// <remarks>
    /// The pair together is the whole repair. The old form was
    /// <c>(shotPower − savePower + 14) / 54</c> with the constants written for a 1..20 scale,
    /// which over 1..100 bolts at 0.50 for any striker better than about 60 against an average
    /// keeper — so a 70 and a 95 were the same finisher. Comparing the two men on the same
    /// -1..1 scale makes the striker's keeper a real question: a great striker against a poor
    /// keeper is about 0.42, and the reverse is about 0.20.
    /// </remarks>
    public const double GoalChanceSwing = 0.22;

    /// <summary>
    /// The band a shot can leave for the target. A poor finisher off a poor pass is
    /// off target more often than not, and never misses when he is alone with the keeper.
    /// </summary>
    public const double MinOnTargetChance = 0.20;
    public const double MaxOnTargetChance = 0.88;

    /// <summary>
    /// What a shot in open play is worth before the striker is read: the base the side's
    /// chance quality moves around.
    /// </summary>
    public const double BaseOnTargetChance = 0.46;

    /// <summary>
    /// How much of the -1..1 gap between the striker and an average man reaches the roll,
    /// and the same gap for the side's chance quality in open play.
    /// </summary>
    public const double OnTargetSwing = 0.22;

    /// <summary>
    /// How much of the side's chance quality — how well it got to the final third, measured
    /// off the two attacks — reaches the shot's chance of being on target.
    /// </summary>
    /// <remarks>
    /// A side that cannot get to the final third still has shots; they are just worse ones,
    /// and this is what makes them worse ones. It is a swing off the *even* fixture rather
    /// than a share of the two attacks, because a share of two attacks is a number between
    /// zero and one whose two ends are "never shoots" and "always on target" — which is not
    /// what either end means.
    /// </remarks>
    public const double ChanceQualityWeight = 0.30;

    /// <summary>
    /// The chance a ball trapped by the keeper comes loose to somebody else, and the
    /// chance that the second shot is the goal the first one was.
    /// </summary>
    public const double ReboundChance = 0.28;
    public const double ReboundGoalChance = 0.24;

    /// <summary>
    /// An own goal is an error, and it comes from exactly one place: a defender doing his
    /// job and getting it wrong, trying to play a ball past his own goalkeeper and putting
    /// it into his own net.
    ///
    /// The chance is per defensive action rather than per match, and a club plays about
    /// thirty of those a match — so this is roughly a tenth of an own goal a match. A thing
    /// that happens twice a season is a story; a thing that happens twice a match is a
    /// rule, and a rule nobody argued about.
    /// </summary>
    public const double OwnGoalFromClearance = 0.003;

    // --- Discipline -------------------------------------------------------------

    /// <summary>
    /// How often a foul is inside the area. It is the only way a penalty is born, so this
    /// is the number that decides how often the eleven metres show up. Around a third of a
    /// match's worth of fouls is closer to the truth than the prototype's one in eight.
    /// </summary>
    public const double PenaltyFromFoulChance = 0.06;

    // --- Energy -----------------------------------------------------------------

    /// <summary>
    /// The floor of the energy ramp, and how steeply it falls at the bottom. See
    /// <see cref="EnergyCurve"/> for why they are two numbers rather than one.
    /// </summary>
    /// <remarks>
    /// The floor is the property the whole model rests on. A ramp that reaches zero lets a
    /// tired man deliver nothing, and nothing is not worse than a squad player — it is
    /// <i>worse than a squad player</i> by as much as he would have been better than one,
    /// which is how energy became a veto over quality instead of a tax on it. At 0.65 a fully
    /// drained player keeps two thirds of himself, so an attribute gap of about two to one is
    /// untouchable by energy whatever the tank.
    /// </remarks>
    public const double EnergyFactorFloor = 0.65;
    public const double EnergyFactorGamma = 1.5;

    /// <summary>
    /// What a tick of running costs a player who is having an ordinary night, before his
    /// age and his knocks. Over a match it is worth about seven points of energy, which
    /// is what makes a man in his thirties feel a season.
    /// </summary>
    public const double EnergyCostPerTick = 0.035;

    /// <summary>
    /// One tick in this many costs double. Football is not a metronome, and a side whose
    /// energy only ever falls by the same amount is a spreadsheet.
    /// </summary>
    public const double SprintChancePerTick = 0.15;

    /// <summary>
    /// Nobody plays a match on empty. The floor is what a spent player still has to give.
    /// </summary>
    public const int EnergyFloorDuringMatch = 35;

    /// <summary>
    /// The minutes a full match lasts, which is what a recovery is measured against: a man
    /// who was on the pitch for all of them has played a match, and a man who was on it for
    /// five has not.
    /// </summary>
    public const int MinutesInAMatch = 90;

    /// <summary>
    /// The energy a man is expected to have when nothing has gone wrong, and the point the
    /// tiredness scale is measured from.
    /// </summary>
    /// <remarks>
    /// A reference and not zero, because tiredness is not a cost charged to everybody: a
    /// player who comes to the penalty at the start of a match is not tired, and a scale
    /// that started at zero would make a squad sitting on eighty look like a squad sitting
    /// on a night shift. Eighty is what a healthy man has in the tank, and it is where the
    /// scale reads nothing either way.
    /// </remarks>
    public const int ReferenceEnergy = 80;

    /// <summary>
    /// How far from the reference the tiredness is measured, in energy points. Sixty means a
    /// man sixty points below the reference is as tired as the scale goes, and a man sixty
    /// points above it is as fresh.
    /// </summary>
    public const int FatigueSpan = 60;

    /// <summary>
    /// How much a penalty moves for a tired man, on the same scale as the skill of the man
    /// taking it. Enough that the last penalty of a match is a different penalty from the
    /// first one, and not so much that a tired taker stops being a taker.
    /// </summary>
    public const double TirednessWeight = 0.10;

    /// <summary>
    /// The least a man who set foot on the pitch gets back, however briefly he was on it. A
    /// cameo is not a match and it is not nothing: he ran out, he warmed up, and he is a
    /// man who has played football today.
    /// </summary>
    public const int MinRecoveryForACameo = 1;

    /// <summary>
    /// The floor after a knock. It is below the match floor because an injury is the one
    /// thing that takes a player past what the rest of the match asks of him.
    /// </summary>
    public const int EnergyFloorAfterInjury = 8;

    // --- Between rounds ---------------------------------------------------------

    /// <summary>
    /// What a player gets back between matches. Playing a match is worth far less than
    /// sitting one out, which is the whole reason a squad is rotated: the recovery band
    /// of a man who played and the band of a man who rested do not overlap.
    /// </summary>
    public const int MinRecoveryAfterPlaying = 3;
    public const int MaxRecoveryAfterPlaying = 7;
    public const int MinRecoveryAfterResting = 11;
    public const int MaxRecoveryAfterResting = 18;

    // --- Injuries ---------------------------------------------------------------

    /// <summary>
    /// The chance of a knock in an action, before the player is even considered. It is a
    /// base rate, added up over the whole side on the pitch, so a bigger eleven gets more
    /// injuries only because more of it is out there playing.
    /// </summary>
    public const double InjuryBaseChance = 0.0035;

    /// <summary>
    /// How much age and tiredness add to that base rate, and the ceiling that stops an
    /// old exhausted player from being the only man on the pitch.
    /// </summary>
    public const double InjuryAgeWeight = 0.006;
    public const double InjuryFatigueWeight = 0.014;
    public const double MaxInjuryChance = 0.16;

    /// <summary>
    /// A player who has already been kicked plays the next action with his other leg.
    /// This is the only multiplier on injury chance, and it is why a light knock is a
    /// consequence rather than a detail.
    /// </summary>
    public const double ReinjuryMultiplier = 1.9;

    /// <summary>
    /// Whether a knock ends the match for him, and what it costs when it does.
    /// </summary>
    public const double SevereInjuryBaseChance = 0.16;
    public const double SevereInjuryAfterKnockChance = 0.34;
    public const double MaxSevereInjuryChance = 0.65;
    public const double OldPlayerSevereBonus = 0.08;
    public const int SevereInjuryAge = 34;

    public const int MinEnergyLostSevere = 28;
    public const int MaxEnergyLostSevere = 48;
    public const int MinEnergyLostLight = 10;
    public const int MaxEnergyLostLight = 24;

    /// <summary>
    /// How many matches of his club a player misses with a serious injury.
    /// </summary>
    public const int MinMatchesOutSevere = 2;
    public const int MaxMatchesOutSevere = 4;

    /// <summary>
    /// The age the injury rate is measured from, and the span it is measured over.
    /// </summary>
    public const int InjuryAgeReference = 16;
    public const double InjuryAgeSpan = 25;

    // --- Substitutions ----------------------------------------------------------

    /// <summary>
    /// How many players a club may replace in a match. It is the same limit for the
    /// manager and for the club the engine plays, because a bench is a bench: the engine
    /// must never be able to give the opposition a fifth change the manager is not allowed
    /// to make.
    /// </summary>
    public const int MaxSubstitutions = 5;

    /// <summary>
    /// How long the game waits at the spot for the manager to name who takes the penalty.
    ///
    /// <para>
    /// Fifteen seconds of waiting, and then the engine sends somebody to the spot by itself.
    /// It is the only window in a match that still stops the clock, because a penalty is the
    /// one decision with nowhere to put the ball until it is answered: the match is at a dead
    /// ball and everybody is looking at the eleven that is going to take it.
    /// </para>
    ///
    /// <para>
    /// It is measured in seconds of waiting and not in match minutes, because the clock is
    /// the thing that has stopped. The count is the backend's and the screen only draws it:
    /// a manager who does not answer is a manager whose match has to finish anyway, and a
    /// rule that lived in the client would be a rule a closed tab could keep forgetting.
    /// </para>
    /// </summary>
    public const int PenaltySelectionSeconds = 15;

    /// <summary>
    /// How long the interval lasts, in seconds, before the second half begins by itself.
    ///
    /// <para>
    /// The break belongs to the manager: twenty seconds to look at the eleven, make a change
    /// or simply let the game go on. It is the backend's twenty seconds and not the manager's
    /// patience — a screen that waits for a button is a screen that a closed tab leaves the
    /// whole world waiting on, and a break that lasts for ever is a fixture that is never
    /// finished and a window that is never closed.
    /// </para>
    /// </summary>
    public const int HalfTimeSeconds = 20;

    /// <summary>
    /// How long a man who cannot carry on waits for the manager to name his replacement.
    ///
    /// <para>
    /// The clock does not stop for this one. The man is off the pitch from the moment the
    /// knock happens, the club is a man short while the window is open, and the engine puts
    /// the best of what is on the bench on for him when the window closes unanswered. A
    /// decision worth a name is worth twenty seconds; it is not worth a stopped match.
    /// </para>
    /// </summary>
    public const int InjuryReplacementSeconds = 20;

    /// <summary>
    /// How long a manager's claim on a match is honoured before the engine answers for him.
    ///
    /// <para>
    /// This is the backstop behind the three windows above: whatever else has been decided,
    /// a match a manager claimed and then abandoned is a match the engine finishes on its
    /// own. Without it a manager who closes the tab leaves a match that stops mid-afternoon
    /// and takes the whole world down with it — the fixture stays owed and the window never
    /// closes.
    /// </para>
    /// </summary>
    public const int ManagerDecisionTimeoutSeconds = 45;

    /// <summary>
    /// A tired player is worth replacing below this energy; a team running on fumes is a
    /// bad look and a worse second half.
    /// </summary>
    public const int EnergyForFatigueSubstitution = 55;

    /// <summary>
    /// A booked player on low energy is one more foul from being sent off, which is a
    /// worse use of the bench than a straight swap.
    /// </summary>
    public const int EnergyForRiskySubstitution = 62;

    /// <summary>
    /// The window after the interval where a club is allowed to correct itself, and the
    /// chance inside it. A change at minute 20 is a different decision from the same
    /// change at minute 80, and the engine is allowed to make it in one of them.
    /// </summary>
    public const int SecondHalfSubWindowStart = 45;
    public const int SecondHalfSubWindowEnd = 55;
    public const double HalftimeSubstitutionChance = 0.18;

    /// <summary>
    /// The chance of reacting to a booked, tired player, and to a plain exhausted one
    /// late on. Both are small: a manager who changes a man every time he looks tired has
    /// no bench.
    /// </summary>
    public const int RiskySubstitutionMinute = 55;
    public const double RiskySubstitutionChance = 0.035;
    public const int FatigueSubstitutionMinute = 72;
    public const double FatigueSubstitutionChance = 0.025;

    // --- Ratings ----------------------------------------------------------------

    /// <summary>
    /// The base of a goalkeeper's ability when his club has had to make one out of an
    /// outfielder. He is not a goalkeeper, so it is a fraction of the same three attributes,
    /// and a team is punished in its own goal for having no cover.
    /// </summary>
    public const double EmergencyKeeperScale = 0.42;
    public const double EmergencyKeeperFloor = 20.0;

    /// <summary>
    /// How much of a keeper's ability is his reflexes rather than his power. Reflexes are
    /// what the role is; the power is what stops him being a mannequin.
    /// </summary>
    public const double KeeperReflexShare = 0.55;

    /// <summary>
    /// What a missing player is worth. A club down a man is not playing the same match,
    /// and the eleven is not the same eleven either.
    /// </summary>
    public const double AbsenceAttackPenalty = 0.045;
    public const double AbsenceMidfieldPenalty = 0.04;
    public const double AbsenceDefensePenalty = 0.05;

    /// <summary>
    /// How much of the goalkeeper's ability is worth as outfield defending. He is the
    /// reason a cross is cleared, not the reason a cross is dangerous.
    /// </summary>
    public const double KeeperShareOfDefense = 0.35;

    /// <summary>
    /// How much of a line's fill is decided by what that line is for, as a share of the
    /// rating. It has to be more than half: a creative midfielder is a handful of points
    /// ahead of a holding one on the general metric, and a minority weight loses that
    /// every time, which is the shape being a label again. The rest of the weight is the
    /// general metric, and that is what keeps a tactic from becoming a list of men the
    /// manager is not allowed to pick.
    /// </summary>
    public const double LineJobWeight = 0.55;

    /// <summary>
    /// The eleven, and the seven behind it.
    ///
    /// They live here rather than beside the service that fills them because they are now two
    /// rules and not one: the kick-off fills them, and a manager writing an order before a
    /// match is checked against them. A second copy of "eleven" in the screen would be a
    /// screen that accepts ten men and a kick-off that then refuses to start.
    /// </summary>
    public const int SquadSize = 11;

    public const int BenchSize = 7;

    // --- Player ratings ---------------------------------------------------------

    /// <summary>
    /// Where a man starts, and what a rating of nothing at all would be. It is not zero: a
    /// footballer who played a full match and was not the worst man on a pitch full of
    /// footballers did not play badly, and a scale that says he did is measuring the wrong
    /// thing. Everything else is a move away from here.
    ///
    /// <para>
    /// It sits a full point above the red line rather than on it, so the ordinary mark is
    /// visibly the middle of the scale and not the edge of the bad half of it. A scale whose
    /// ordinary evening is the same number as the line red begins at has no middle, and every
    /// card in a season then reads as a verdict on the man rather than as a reading of his
    /// evening.
    /// </para>
    /// </summary>
    public const double RatingBaseline = 7.0;

    public const double RatingFloor = 0.0;
    public const double RatingCeiling = 10.0;

    /// <summary>
    /// Below this a man is told so, in red, and it is a point under the ordinary mark rather
    /// than on it.
    ///
    /// <para>
    /// The gap is deliberate and is the reason the two lines are two lines. A man on the
    /// ordinary mark has not done badly — he has done nothing the card can hold against
    /// him — so red is reserved for men the number is actually against. Were the line the
    /// baseline, the only way into red would be to be worse than doing nothing at all, which
    /// is not the complaint a manager is making about the man in front of him, and the scale
    /// would have no ordinary evening in it to read.
    /// </para>
    /// </summary>
    public const double RatingRedBelow = 6.0;

    /// <summary>
    /// Where a good evening starts, and it is inclusive so that the number on the card and
    /// the colour beside it agree: a man marked 8.0 is shown as a good evening rather than as
    /// an ordinary one that happens to round to eight.
    /// </summary>
    public const double RatingGreen = 8.0;

    /// <summary>
    /// The blue diamond is the ceiling itself and not a band above it, so there is exactly
    /// one way to wear it and a player is never told he was better than the best.
    /// </summary>
    public const double RatingDiamond = 10.0;

    /// <summary>
    /// How much of a reading survives being shrunk back towards the baseline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ninety-minute match is a small sample of a large number of things, and without this
    /// a single extraordinary thirty minutes prints a 9.8 — a number that says a man was
    /// flawless rather than that he had an excellent afternoon. It also keeps the bands honest:
    /// the rare perfect on a shrinking scale is still 10, so the top of the scale is reachable,
    /// but the middle of it belongs to ordinary days rather than to ordinary samples.
    /// </para>
    /// </remarks>
    public const double RatingShrink = 0.85;

    /// <summary>
    /// What each thing that happened is worth, in points of rating.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They are the volume, and they are deliberately blunt: a goal is a goal whether the man
    /// who scored it was expected to or not, and the question of expectation is a separate
    /// axis rather than a discount on this one. Charging a striker less for a goal because he
    /// is good at them would mean the best striker in the world could not have a good match.
    /// </para>
    /// <para>
    /// All twelve were moved by a single factor of 1.5 from a calibration measured over a
    /// hundred played matches, because what they produced was a card that separated nobody:
    /// two thousand men averaged 6.25, a third of them (32.95%) came out in red, only 1.37%
    /// reached a good evening and not one of the two thousand reached the top of the scale. The
    /// same hundred at this factor averages 7.39, puts 1.69% in red, gives 20.90% a good
    /// evening and has 1.37% at the ceiling. One factor rather than twelve numbers picked one
    /// at a time, so the ratio between a goal and a card is exactly what it was before the
    /// calibration was read again — what changed is how far a match moves a man, not what
    /// things are worth relative to each other.
    /// </para>
    /// <para>
    /// The three surprise weights below were deliberately not moved with them. Surprise is a
    /// different axis: it is measured against the chance the engine had already given him
    /// rather than against the size of the thing that happened, and it is capped on its own.
    /// Moving both axes together would have widened the spread well past what was asked for
    /// and would have made the two axes indistinguishable — a big number and a surprising
    /// one would have become the same claim, said twice.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The flat weight of a goal, and it is smaller than the occasion because it is not the
    /// whole of a goal. A goal also arrives as a shot on target and as the largest single
    /// surprise in a match, and the three are counted separately, so a weight of its own large
    /// enough to carry the evening would count the same ninety minutes three times.
    ///
    /// <para>
    /// On these numbers a goal read on its own is 8.2, which is already a good evening, and
    /// the same goal as it really arrives — with the shot on target that made it — is
    /// 8.6. A brace with the two shots that made it is 10.2 before the scale clamps it, so a
    /// brace is the ceiling and a hat-trick is worth no more than a brace. The old weights put
    /// the same three evenings at 6.8, 7.1 and 8.9, so the room between a good evening and the
    /// top of the scale has gone from about three points to about one. That is the price of the
    /// 1.5: the factor bought a scale that tells an ordinary man from a good one, and the
    /// headroom between a good evening and the best evening went with it.
    /// </para>
    /// </summary>
    public const double RatingGoal = 1.425;
    public const double RatingAssist = 1.05;
    public const double RatingSave = 0.33;
    public const double RatingShotOnTarget = 0.45;
    public const double RatingShotOffTarget = -0.06;
    public const double RatingDuelWon = 0.105;
    public const double RatingDuelLost = -0.075;
    public const double RatingFoul = -0.105;
    public const double RatingCorner = 0.06;
    public const double RatingYellowCard = -0.45;
    public const double RatingRedCard = -2.1;
    public const double RatingOwnGoal = -1.5;

    /// <summary>
    /// What one moment is worth when it was better or worse than the engine had already said
    /// it would be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the axis that answers "was he above his profile tonight" without double counting.
    /// The engine has already rolled a probability for every duel and every shot — the
    /// probability the carrier beats the marker, the probability the shot finds the target —
    /// and has thrown it away. Comparing the roll against the chance is the whole measurement:
    /// a striker who converts the chances he should have missed is playing above his profile, and
    /// one who misses the ones he should have made is playing below it, and neither fact is
    /// visible in the number of goals.
    /// </para>
    /// <para>
    /// A duel is worth less than a shot because a duel is a smaller thing to have got right.
    /// The sum is capped, so a long match of many small surprises cannot outvote a goal.
    /// </para>
    /// </remarks>
    // A shot is very nearly a coin flip — the chance of finding the target sits around one in
    // two — so every shot a striker takes is a large deviation from its own chance, positive
    // or negative. A heavy weight therefore made a striker who missed three look worse than
    // one who missed none by more than a goal is worth, and a man having three shots is an
    // ordinary evening rather than a bad one. The weights are small for that reason and not
    // because the measurement matters less.
    public const double RatingDuelSurprise = 0.10;
    public const double RatingShotSurprise = 0.15;
    public const double RatingPenaltySurprise = 0.50;
    public const double RatingMaxSurprise = 1.5;

    /// <summary>
    /// How much a man loses for being on the pitch and in nothing, and the involvement each
    /// role is expected to have.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the "ten minutes without touching the ball" rule, and it is measured against
    /// what the man's own job asks of him rather than against a clock. A centre-back being
    /// unmarked is what playing well looks like for him; a ten minute gap in his match is his
    /// ordinary Tuesday, and a flat penalty would make the best defenders in the world grade
    /// below the worst strikers. The rate is also squared, so a man slightly below his role's
    /// rate is barely marked and a man involved in nothing is.
    /// </para>
    /// <para>
    /// The rates are per ninety and are the engine's own, not football's: this model resolves
    /// about two events per player per match rather than the fifty touches a real match has,
    /// so a rate drawn from real football would put every man in the world under water.
    /// </para>
    /// </remarks>
    public const double RatingInactivityWeight = 0.8;

    /// <summary>
    /// The most a man can be marked down for being in nothing at all, and it is three numbers
    /// rather than one because the complaint is not the same in four places.
    /// </summary>
    /// <remarks>
    /// A forward who is on the pitch for ninety minutes and touches nothing is a real problem
    /// and is marked as one. A centre-back in the same evening is doing the job, which is to
    /// be unmarked; a goalkeeper who is barely involved is barely involved, and a rule that
    /// said otherwise would be telling a manager his keeper had a bad night every time the
    /// back line kept it quiet. One shared cap would make all four of them equal, and the
    /// first thing anyone would say about the result is that it cannot tell those apart.
    /// </remarks>
    public const double RatingMaxInactivityOutfield = 0.60;
    public const double RatingMaxInactivityDefense = 0.30;
    public const double RatingMaxInactivityGoalkeeper = 0.10;
    // The floors, measured rather than guessed: over a hundred played matches the median
    // outfielder was involved in three things and the bottom quarter in one or none, so a
    // floor of two leaves an ordinary evening unmarked and catches the tail. A goalkeeper has
    // none, because he is measured on his saves.
    public const double RatingInvolvementsPerNinetyAttack = 2.0;
    public const double RatingInvolvementsPerNinetyMidfield = 2.0;
    public const double RatingInvolvementsPerNinetyDefense = 2.0;
    public const double RatingInvolvementsPerNinetyGoalkeeper = 0.0;

    /// <summary>
    /// The shortest match a man has to play to be given a rating at all.
    /// </summary>
    /// <remarks>
    /// A man off the bench for the last five minutes has not had a performance, he has had a
    /// cameo, and a number printed beside his name would be a number nobody could have earned
    /// or deserved. This is a floor on the sample rather than a judgement about the player.
    /// </remarks>
    public const int RatingMinimumMinutes = 5;
}
