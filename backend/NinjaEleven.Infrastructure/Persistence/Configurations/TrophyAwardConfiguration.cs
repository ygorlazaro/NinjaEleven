using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class TrophyAwardConfiguration : IEntityTypeConfiguration<TrophyAward>
{
    public void Configure(EntityTypeBuilder<TrophyAward> builder)
    {
        builder.ToTable("trophy_awards");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(t => t.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(t => t.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CompetitionSeason>()
            .WithMany()
            .HasForeignKey(t => t.CompetitionSeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Division>()
            .WithMany()
            .HasForeignKey(t => t.DivisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.Kind)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(t => t.Position).IsRequired();

        // The prize is money in limos. It is zero for every trophy until the first one is
        // paid, and the column exists so that paying one is a data change and not a migration.
        builder.Property(t => t.PrizeMoney).HasPrecision(12, 2).IsRequired().HasDefaultValue(0m);

        // A club's shelf, read newest first, and the same club's shelf in a given season.
        builder.HasIndex(t => new { t.TeamId, t.SeasonId });
    }
}
