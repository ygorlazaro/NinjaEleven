using NinjaEleven.Domain.Matches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class MatchStatisticsConfiguration : IEntityTypeConfiguration<MatchStatistics>
{
    public void Configure(EntityTypeBuilder<MatchStatistics> builder)
    {
        builder.ToTable("match_statistics");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne<Match>()
            .WithOne()
            .HasForeignKey<MatchStatistics>(s => s.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.MatchId).ValueGeneratedNever();
    }
}
