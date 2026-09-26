using FootballManager.Domain.Competitions;
using FootballManager.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

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

        builder.HasIndex(cs => new { cs.CompetitionId, cs.SeasonId }).IsUnique();
    }
}
