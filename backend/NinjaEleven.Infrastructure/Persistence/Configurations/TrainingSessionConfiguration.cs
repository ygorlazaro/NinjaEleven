using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Players;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> builder)
    {
        builder.ToTable("training_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne<Domain.Players.Player>()
            .WithMany()
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Teams.Team>()
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Seasons.Season>()
            .WithMany()
            .HasForeignKey(s => s.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        // The matchday is kept for reading, not for integrity: a calendar that is redrawn
        // replaces its matchdays, and a session that had already happened does not become
        // unreal because the day it was spent on was redrawn out from under the world.
        builder.HasOne<Domain.Competitions.MatchDay>()
            .WithMany()
            .HasForeignKey(s => s.MatchDayId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(s => s.Attribute).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(s => s.Fee).HasPrecision(14, 2).IsRequired();
        builder.Property(s => s.EnergyCost).IsRequired();

        // The daily allowance is counted by reading this man's rows for this day, so the index
        // is on exactly the pair the count asks about. It is the one query the rule makes on
        // every click, and it is a count over a table that grows for ever — a club's whole
        // training history is small, but a world's is not.
        builder.HasIndex(s => new { s.TeamId, s.Day });
        builder.HasIndex(s => new { s.PlayerId, s.Day });
        builder.HasIndex(s => new { s.SeasonId, s.TeamId, s.Day });

        // And the count is the last line of defence rather than the only one. Two sessions
        // arriving at once would both read the same allowance and both be told yes; unique
        // over the man, the day and the session's own place in his day means the second one
        // cannot be written, so his allowance holds whatever the read said.
        builder.HasIndex(s => new { s.PlayerId, s.Day, s.Ordinal }).IsUnique();
    }
}
