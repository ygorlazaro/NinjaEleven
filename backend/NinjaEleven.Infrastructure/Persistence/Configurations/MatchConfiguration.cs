using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("matches");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne<Fixture>()
            .WithMany()
            .HasForeignKey(m => m.FixtureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.Half)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.CurrentMinute).IsRequired();
        builder.Property(m => m.HomeScore).IsRequired();
        builder.Property(m => m.AwayScore).IsRequired();
        builder.Property(m => m.Sequence).IsRequired();
        builder.Property(m => m.Seed).IsRequired();

        // A match belongs to a competition and to a window of a matchday. The two are read
        // together constantly — a table counts league games, an aggregate counts cup legs, and
        // energy is recovered per window — so a match row that does not say which it is cannot
        // be read without going back up the fixture and the round.
        builder.Property(m => m.CompetitionType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(m => m.Window).IsRequired().HasDefaultValue(CompetitionRules.ChampionshipWindow);

        // The gate, in three numbers rather than one. A club's books need to know what it took
        // and what it gave away, and one gross figure cannot answer that.
        builder.Property(m => m.Attendance).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.TicketPrice).HasPrecision(10, 2).IsRequired().HasDefaultValue(0m);
        builder.Property(m => m.GrossRevenue).HasPrecision(12, 2).IsRequired().HasDefaultValue(0m);
        builder.Property(m => m.HomeRevenue).HasPrecision(12, 2).IsRequired().HasDefaultValue(0m);
        builder.Property(m => m.AwayRevenue).HasPrecision(12, 2).IsRequired().HasDefaultValue(0m);

        // A fixture can hold more than one match: a match interrupted by a restart is
        // abandoned and the fixture is played again, so the old row stays as history.
        // The live match of a fixture is the newest row that was not abandoned.
        builder.HasIndex(m => m.FixtureId).HasDatabaseName("ix_matches_fixture_id");
        builder.HasIndex(m => m.Status);

        // Whose working memory this match belongs to, and how long that memory is honoured
        // after the last tick. The world is played by more than one process now, so a match
        // has to say which one is in the middle of it.
        builder.Property(m => m.SessionHost).HasMaxLength(64).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(m => m.SessionHeartbeatAt);
        builder.HasIndex(m => m.SessionHost);

        // One live match per fixture, and the database is the one that says so.
        //
        // Two processes that woke at the same instant can both read a scheduled fixture and
        // both decide to play it, and reading first and writing second is how a fixture ends
        // up with two copies of the same evening on the pitch. The second insert is refused
        // here rather than by a check that another process might not have made yet, which is
        // the same reason the round claim is taken under a row lock. Finished and abandoned
        // rows are outside the index, so a replay of a fixture whose match went wrong is
        // still allowed.
        builder.HasIndex(m => m.FixtureId)
            .IsUnique()
            .HasFilter("status NOT IN ('Finished', 'Abandoned')")
            .HasDatabaseName("ux_matches_live_fixture");
    }
}
