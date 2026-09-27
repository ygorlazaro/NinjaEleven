using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class CupTieConfiguration : IEntityTypeConfiguration<CupTie>
{
    public void Configure(EntityTypeBuilder<CupTie> builder)
    {
        builder.ToTable("cup_ties");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.HasOne<CompetitionSeason>()
            .WithMany()
            .HasForeignKey(t => t.CompetitionSeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(t => t.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(t => t.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Fixture>()
            .WithMany()
            .HasForeignKey(t => t.FirstLegFixtureId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Fixture>()
            .WithMany()
            .HasForeignKey(t => t.SecondLegFixtureId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(t => t.WinnerTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(t => t.LoserTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.RoundNumber).IsRequired();

        // A tie is looked up by the round it belongs to far more often than by its two clubs,
        // and the bracket is read round by round.
        builder.HasIndex(t => new { t.CompetitionSeasonId, t.RoundNumber });
    }
}
