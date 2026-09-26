using FootballManager.Domain.Competitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

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

        builder.Property(r => r.Number).IsRequired();

        builder.HasIndex(r => new { r.CompetitionSeasonId, r.Number }).IsUnique();
    }
}
