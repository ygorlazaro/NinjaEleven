using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class CompetitionParticipantConfiguration : IEntityTypeConfiguration<CompetitionParticipant>
{
    public void Configure(EntityTypeBuilder<CompetitionParticipant> builder)
    {
        builder.ToTable("competition_participants");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasOne<CompetitionSeason>()
            .WithMany()
            .HasForeignKey(p => p.CompetitionSeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.CompetitionSeasonId, p.TeamId }).IsUnique();
    }
}
