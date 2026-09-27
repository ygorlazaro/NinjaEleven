using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class CompetitionSeasonConfiguration : IEntityTypeConfiguration<CompetitionSeason>
{
    public void Configure(EntityTypeBuilder<CompetitionSeason> builder)
    {
        builder.ToTable("competition_seasons");

        builder.HasKey(cs => cs.Id);
        builder.Property(cs => cs.Id).ValueGeneratedNever();

        builder.HasOne<Competition>()
            .WithMany()
            .HasForeignKey(cs => cs.CompetitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(cs => cs.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        // Which tier this edition is the table of. It is null for a cup and a Supercup,
        // because a cup is drawn from the whole pyramid and does not belong to one tier.
        builder.HasOne<Division>()
            .WithMany()
            .HasForeignKey(cs => cs.DivisionId)
            .OnDelete(DeleteBehavior.Restrict);

        // A competition can run more than once in a season — the league runs once per tier —
        // so the division is part of what makes an edition unique. A cup has no division and
        // its season is enough to tell it apart.
        builder.HasIndex(cs => new { cs.CompetitionId, cs.SeasonId, cs.DivisionId }).IsUnique();
        builder.HasIndex(cs => cs.SeasonId);
    }
}
