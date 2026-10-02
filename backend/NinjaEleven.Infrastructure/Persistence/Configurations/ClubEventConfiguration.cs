using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class ClubEventConfiguration : IEntityTypeConfiguration<ClubEvent>
{
    public void Configure(EntityTypeBuilder<ClubEvent> builder)
    {
        builder.ToTable("club_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(e => e.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade and not Restrict, because a season that never happened cannot have had
        // moments recorded inside it: the row is a fact about the club and the season is only
        // where on the clock it landed. Throwing away the season takes the season's own facts
        // with it, and a club's page is not one of them.
        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(e => e.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Kind)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(e => e.PreviousValue).HasMaxLength(120);
        builder.Property(e => e.NewValue).HasMaxLength(120);

        // A club's page reads its own moments, newest first, and it does that on every visit —
        // so the index is the one the read uses rather than the one the write would prefer.
        builder.HasIndex(e => new { e.TeamId, e.OccurredAt });
    }
}