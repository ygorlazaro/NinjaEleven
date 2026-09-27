using NinjaEleven.Domain.Competitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class RoundConfiguration : IEntityTypeConfiguration<Round>
{
    public void Configure(EntityTypeBuilder<Round> builder)
    {
        builder.ToTable("rounds");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.HasOne<CompetitionSeason>()
            .WithMany()
            .HasForeignKey(r => r.CompetitionSeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        // A round is a window of a matchday now, so it is only meaningful once it has been
        // put on the calendar. It stays nullable while a season is being built, because a
        // competition that has fixtures but no dates yet is a season mid-construction.
        builder.HasOne<MatchDay>()
            .WithMany()
            .HasForeignKey(r => r.MatchDayId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(r => r.Number).IsRequired();
        builder.Property(r => r.Window).IsRequired().HasDefaultValue(CompetitionRules.ChampionshipWindow);

        // When the window closed. It is the guard that stops the window's energy recovery from
        // being applied twice, and it is why recovery is a thing that happens exactly once.
        builder.Property(r => r.CompletedAt);

        builder.HasIndex(r => new { r.CompetitionSeasonId, r.Number }).IsUnique();
        builder.HasIndex(r => r.MatchDayId);
    }
}
