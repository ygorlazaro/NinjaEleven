using FootballManager.Domain.Players;
using FootballManager.Domain.Seasons;
using FootballManager.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

public class PlayerSeasonStateConfiguration : IEntityTypeConfiguration<PlayerSeasonState>
{
    public void Configure(EntityTypeBuilder<PlayerSeasonState> builder)
    {
        builder.ToTable("player_season_states");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(s => s.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.Energy).IsRequired();
        builder.Property(s => s.Goals).IsRequired();
        builder.Property(s => s.YellowCards).IsRequired();
        builder.Property(s => s.RedCards).IsRequired();
        builder.Property(s => s.SuspensionMatches).IsRequired();
        builder.Property(s => s.Injury)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(s => s.InjuryMatchesRemaining).IsRequired();

        builder.HasIndex(s => new { s.PlayerId, s.SeasonId }).IsUnique();
        builder.HasIndex(s => new { s.SeasonId, s.TeamId });
    }
}
