using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class StadiumConstructionConfiguration : IEntityTypeConfiguration<StadiumConstruction>
{
    public void Configure(EntityTypeBuilder<StadiumConstruction> builder)
    {
        builder.ToTable("stadium_constructions");

        builder.HasKey(construction => construction.Id);
        builder.Property(construction => construction.Id).ValueGeneratedNever();

        builder.HasOne<Stadium>()
            .WithMany()
            .HasForeignKey(construction => construction.StadiumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(construction => construction.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(construction => construction.ClubId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(construction => construction.Seats).IsRequired();
        builder.Property(construction => construction.Cost).HasPrecision(18, 2);
        builder.Property(construction => construction.Rounds).IsRequired();
        builder.Property(construction => construction.StartedAfterRound).IsRequired();
        builder.Property(construction => construction.StartedAt).IsRequired();

        // One ground builds one thing at a time. It is the guard a club starting a second project
        // while the first is in the building site would hit, and it is a unique index rather
        // than a check so that a settlement run twice cannot quietly add a second open row.
        builder.HasIndex(construction => construction.StadiumId)
            .IsUnique()
            .HasFilter("completed_at IS NULL")
            .HasDatabaseName("ix_stadium_constructions_one_open_per_ground");

        // The settlement's read: every open project in the world, oldest first so a matchday
        // that settles several grounds adds the seats in the order the work was paid for.
        builder.HasIndex(construction => construction.CompletedAt);
    }
}