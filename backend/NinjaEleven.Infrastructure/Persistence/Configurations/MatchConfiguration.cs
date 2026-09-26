using NinjaEleven.Domain.Enums;
using NinjaEleven.Domain.Matches;
using NinjaEleven.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

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

        // A fixture can hold more than one match: a match interrupted by a restart is
        // abandoned and the fixture is played again, so the old row stays as history.
        // The live match of a fixture is the newest row that was not abandoned.
        builder.HasIndex(m => m.FixtureId);
        builder.HasIndex(m => m.Status);
    }
}
