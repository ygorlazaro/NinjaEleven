using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class TeamFanBaseConfiguration : IEntityTypeConfiguration<TeamFanBase>
{
    public void Configure(EntityTypeBuilder<TeamFanBase> builder)
    {
        builder.ToTable("team_fan_bases");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(f => f.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(f => f.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.Supporters).IsRequired();
        builder.Property(f => f.OpeningSupporters).IsRequired();
        builder.Property(f => f.PeakSupporters).IsRequired();
        builder.Property(f => f.UpdatedAt).IsRequired();

        // One row per club per season. This is the guard that stops a season being closed twice
        // from moving a crowd twice: the season close runs off a window that can be closed again
        // by a process that comes back after a weekend down, and without this index the second
        // close would find the first close's row and add its growth on top of it.
        builder.HasIndex(f => new { f.TeamId, f.SeasonId }).IsUnique();

        // The other read: "every club in this season", which is how the close writes and how a
        // league table reads the whole country at once.
        builder.HasIndex(f => f.SeasonId);
    }
}