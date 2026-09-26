using FootballManager.Domain.Enums;
using FootballManager.Domain.Matches;
using FootballManager.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("matches");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne<Fixture>()
            .WithMany()
            .HasForeignKey(m => m.FixtureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.Half)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.CurrentMinute).IsRequired();
        builder.Property(m => m.HomeScore).IsRequired();
        builder.Property(m => m.AwayScore).IsRequired();
        builder.Property(m => m.Sequence).IsRequired();
        builder.Property(m => m.Seed).IsRequired();

        builder.HasIndex(m => m.FixtureId).IsUnique();
        builder.HasIndex(m => m.Status);
    }
}
