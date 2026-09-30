using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Finance;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class FinanceMovementConfiguration : IEntityTypeConfiguration<FinanceMovement>
{
    public void Configure(EntityTypeBuilder<FinanceMovement> builder)
    {
        builder.ToTable("finance_movements");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne<Domain.Teams.Team>()
            .WithMany()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Seasons.Season>()
            .WithMany()
            .HasForeignKey(m => m.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Matches.Match>()
            .WithMany()
            .HasForeignKey(m => m.MatchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(m => m.MatchDayNumber);
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Reference).HasMaxLength(160);
        builder.Property(m => m.Amount).HasPrecision(14, 2).IsRequired();
        builder.Property(m => m.BalanceAfter).HasPrecision(14, 2).IsRequired();

        // The club's book is read as one club's book, newest line first, and the line written
        // last is the one the balance comes from. Ordering by the sequence rather than by the
        // day is what makes that right: the balance a club is handed to open a season belongs
        // to no day at all, and ordering by the day would put a null day first and print the
        // opening line of a season above everything the season earned.
        builder.HasIndex(m => new { m.TeamId, m.Sequence });
        builder.HasIndex(m => new { m.TeamId, m.SeasonId, m.Sequence });
    }
}
