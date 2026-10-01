using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

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
        builder.Property(m => m.ContractSeasons)
            .IsRequired()
            .HasDefaultValue(Domain.Finance.FinanceRules.DefaultContractSeasons);
        builder.Property(m => m.StartSeasonNumber)
            .IsRequired()
            .HasDefaultValue(1);
        builder.Property(m => m.ShirtNumber);

        builder.HasIndex(m => m.PlayerId);
        builder.HasIndex(m => new { m.TeamId, m.StartDate });

        // The uniqueness is the database's and not only the service's, and the filter is what
        // makes it possible at all: a membership row is never deleted, so a club that signed
        // a striker in 2024 and signs him again in 2027 keeps both contracts, and a plain
        // unique index on (club, number) would forbid the club from ever using that number
        // again. Only the live contracts are a dressing room.
        builder.HasIndex(m => new { m.TeamId, m.ShirtNumber })
            .IsUnique()
            .HasFilter("\"end_date\" IS NULL AND \"shirt_number\" IS NOT NULL");
    }
}
