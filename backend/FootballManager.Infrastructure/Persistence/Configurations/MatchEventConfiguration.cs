using FootballManager.Domain.Matches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

public class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> builder)
    {
        builder.ToTable("match_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.HasOne<Match>()
            .WithMany()
            .HasForeignKey(e => e.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Sequence).IsRequired();
        builder.Property(e => e.Minute).IsRequired();
        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(e => e.HomeScore).IsRequired();
        builder.Property(e => e.AwayScore).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(e => new { e.MatchId, e.Sequence }).IsUnique();
    }
}
