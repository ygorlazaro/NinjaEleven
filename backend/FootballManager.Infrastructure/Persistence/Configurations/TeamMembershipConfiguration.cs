using FootballManager.Domain.Players;
using FootballManager.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

public class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("team_memberships");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(m => m.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.StartDate).HasColumnType("date").IsRequired();
        builder.Property(m => m.EndDate).HasColumnType("date");

        builder.HasIndex(m => m.PlayerId);
        builder.HasIndex(m => new { m.TeamId, m.StartDate });
    }
}
