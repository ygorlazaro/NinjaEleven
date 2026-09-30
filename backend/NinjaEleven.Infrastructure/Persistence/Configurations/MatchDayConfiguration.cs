using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Competitions;
using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class MatchDayConfiguration : IEntityTypeConfiguration<MatchDay>
{
    public void Configure(EntityTypeBuilder<MatchDay> builder)
    {
        builder.ToTable("match_days");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(d => d.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(d => d.Number).IsRequired();
        builder.Property(d => d.Date).HasColumnType("date").IsRequired();

        builder.HasIndex(d => new { d.SeasonId, d.Number }).IsUnique();

        // The world asks "which days are in reach" on every tick of every scheduler job, and
        // the answer is a range of dates. An index on the date is what keeps that question a
        // range scan rather than a read of every season the world has ever played.
        builder.HasIndex(d => d.Date);
    }
}
