using NinjaEleven.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(120).IsRequired();
        builder.Property(t => t.ShortName).HasMaxLength(20).IsRequired();
        builder.Property(t => t.PrimaryColor).HasMaxLength(9).IsRequired();
        builder.Property(t => t.SecondaryColor).HasMaxLength(9).IsRequired();
        builder.Property(t => t.Rating).IsRequired();
        builder.Property(t => t.IsManagerClub).IsRequired();

        // The crest and the two shirts are a manager's choices rather than a club's facts about
        // itself, so they are stored as the JSON of the choice and never as a second copy of the
        // design's shape. A club with no crest has NULL rather than an empty document, because
        // an empty string is not JSON and a jsonb column cannot hold one. The three computed
        // properties are how a screen reads them back and they have nothing to map.
        builder.Property(t => t.CrestJson).HasColumnType("jsonb");
        builder.Property(t => t.HomeKitJson).HasColumnType("jsonb");
        builder.Property(t => t.AwayKitJson).HasColumnType("jsonb");
        builder.Ignore(t => t.Crest);
        builder.Ignore(t => t.HomeKit);
        builder.Ignore(t => t.AwayKit);

        builder.HasOne(t => t.Stadium)
            .WithOne()
            .HasForeignKey<Team>(t => t.StadiumId)
            .IsRequired(false);

        builder.HasOne(t => t.ActiveSponsorContract)
            .WithMany()
            .HasForeignKey(t => t.ActiveSponsorContractId)
            .IsRequired(false);

        builder.HasIndex(t => t.Name);
    }
}
