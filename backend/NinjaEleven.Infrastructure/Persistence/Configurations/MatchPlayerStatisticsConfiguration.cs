using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Matches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class MatchPlayerStatisticsConfiguration : IEntityTypeConfiguration<MatchPlayerStatistics>
{
    public void Configure(EntityTypeBuilder<MatchPlayerStatistics> builder)
    {
        builder.ToTable("match_player_statistics");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.MatchId).ValueGeneratedNever();
        builder.Property(s => s.PlayerId).ValueGeneratedNever();
        builder.Property(s => s.TeamId).ValueGeneratedNever();

        // The minutes he was actually out there, which is what a recovery is measured against
        // and what tells a starter taken off at the sixtieth from a substitute who came on at
        // the sixtieth. See MatchPlayerStatistics.MinutesPlayed.
        builder.Property(s => s.MinutesPlayed).IsRequired();

        builder.HasOne<Match>()
            .WithMany()
            .HasForeignKey(s => s.MatchId)
            // A statistic of a match that no longer exists is a statistic of nothing.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        // One line per player per match. This is what stops a match that was re-saved — a
        // loop retry, a second finish — from doubling a striker's season in the history.
        builder.HasIndex(s => new { s.MatchId, s.PlayerId }).IsUnique();

        // The history is always read in one player's order, either a season or the whole
        // career, so the two columns that decide that are indexed together.
        builder.HasIndex(s => new { s.PlayerId, s.SeasonId });
    }
}
