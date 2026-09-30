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
}
