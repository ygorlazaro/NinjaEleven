using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NinjaEleven.Domain.Matches;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class TeamMatchPlanConfiguration : IEntityTypeConfiguration<TeamMatchPlan>
{
    public void Configure(EntityTypeBuilder<TeamMatchPlan> builder)
    {
        builder.ToTable("team_match_plans");

        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();

        builder.HasOne<Domain.Teams.Team>()
            .WithMany()
            .HasForeignKey(plan => plan.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Seasons.Season>()
            .WithMany()
            .HasForeignKey(plan => plan.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(plan => plan.TacticCode).HasMaxLength(16).IsRequired();

        // The eleven and the bench are stored as they were ordered, because the order is the
        // manager's decision and not an accident of a query: it is what decides who plays
        // which line. A set would lose it and a club would turn up with its eleven sorted by
        // whatever the database felt like returning first.
        //
        // jsonb rather than a Postgres array because an empty list and a null are different
        // answers here — "no eleven named" is a plan that only names a shape — and an array
        // column makes the first of those indistinguishable from the second.
        var idList = new ValueConverter<IReadOnlyList<Guid>, string>(
            ids => JsonSerializer.Serialize(ids),
            json => JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>());

        // The comparer, and it is not optional. A converted collection is compared by
        // reference by default, so a plan read back from the database was never "equal" to
        // the plan that was written — and a context that believes the entity it is holding is
        // unchanged refuses the save that would have corrected it. Two saves in a row would
        // then both be no-ops, and a manager's second change would be a button that does
        // nothing with no message, which is the one bug a manager cannot argue with.
        //
        // Compared element by element, in order: the eleven is an ordered list and a set
        // comparison would call two different managers' decisions the same decision.
        var idComparer = new ValueComparer<IReadOnlyList<Guid>>(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            ids => ids.Aggregate(0, (hash, id) => HashCode.Combine(hash, id.GetHashCode())),
            ids => ids.ToList());

        builder.Property(plan => plan.StarterIds)
            .HasColumnType("jsonb")
            .HasConversion(idList)
            .Metadata.SetValueComparer(idComparer);
        builder.Property(plan => plan.BenchIds)
            .HasColumnType("jsonb")
            .HasConversion(idList)
            .Metadata.SetValueComparer(idComparer);

        // One plan per club per season. It is what makes "the plan" a single answer rather
        // than the newest of several, and the constraint is what makes it so when two managers'
        // saves land at once — the second fails instead of quietly becoming a second opinion
        // about how this club plays.
        builder.HasIndex(plan => new { plan.TeamId, plan.SeasonId }).IsUnique();
    }
}