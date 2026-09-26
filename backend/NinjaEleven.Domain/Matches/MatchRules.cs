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

    /// <summary>
    /// A midfield duel, which is everything above <see cref="ShotLimit"/>: a minute with
    /// no shot in it is a minute of passing, pressing and second balls.
    /// </summary>
    public const double DuelSuccessChance = 0.56;

    // --- Shooting ---------------------------------------------------------------

    /// <summary>
    /// The band a shot can leave. A shot is not a coin flip, but the keeper is a real
    /// obstacle, so no combination of attributes can turn it into a certainty.
    /// </summary>
    public const double MinGoalChance = 0.12;
    public const double MaxGoalChance = 0.50;

    /// <summary>
    /// The band a shot can leave for the target. A poor finisher off a poor pass is
    /// off target more often than not, and never misses when he is alone with the keeper.
    /// </summary>
    public const double MinOnTargetChance = 0.20;
    public const double MaxOnTargetChance = 0.88;

    /// <summary>
    /// How much of a team's attacking strength converts into a shot on target. A side
    /// that never gets to the final third still has shots; they are just worse ones.
    /// </summary>
    public const double OnTargetFromStrength = 0.98;
    public const double OnTargetSwing = 0.22;

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
    /// The average attribute of the 1..20 scale, and the distance from it to the top. They
    /// turn an attribute into a -1..1 factor around an average player.
    /// </summary>
    public const double ReferenceAttribute = 13.0;
    public const double AttributeSpan = 7.0;

    /// <summary>
    /// The base of a goalkeeper's ability when his club has had to make one out of an
    /// outfielder. He is not a goalkeeper, so it is a small fraction of the same three
    /// attributes, and a team is punished in its own goal for having no cover.
    /// </summary>
    public const double EmergencyKeeperScale = 0.42;
    public const double EmergencyKeeperFloor = 2.0;

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
