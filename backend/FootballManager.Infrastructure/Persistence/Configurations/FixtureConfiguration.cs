using FootballManager.Domain.Competitions;
using FootballManager.Domain.Matches;
using FootballManager.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

public class FixtureConfiguration : IEntityTypeConfiguration<Fixture>
{
    public void Configure(EntityTypeBuilder<Fixture> builder)
    {
        builder.ToTable("fixtures");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.HasOne<Round>()
            .WithMany()
            .HasForeignKey(f => f.RoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(f => f.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(f => f.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(f => f.RoundId);
        builder.HasIndex(f => f.HomeTeamId);
        builder.HasIndex(f => f.AwayTeamId);
    }
}
