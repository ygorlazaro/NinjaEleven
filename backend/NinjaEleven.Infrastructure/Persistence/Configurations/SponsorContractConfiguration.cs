using NinjaEleven.Domain.Sponsors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class SponsorContractConfiguration : IEntityTypeConfiguration<SponsorContract>
{
    public void Configure(EntityTypeBuilder<SponsorContract> builder)
    {
        builder.ToTable("sponsor_contracts");

        builder.HasKey(contract => contract.Id);
        builder.Property(contract => contract.Id).ValueGeneratedNever();

        builder.Property(contract => contract.SponsorId).IsRequired();
        builder.Property(contract => contract.TeamId).IsRequired();
        builder.Property(contract => contract.SeasonId).IsRequired();

        builder.Property(contract => contract.PerMatchFee).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(contract => contract.ContractMatches).IsRequired();
        builder.Property(contract => contract.MatchesPlayed).IsRequired().HasDefaultValue(0);
        builder.Property(contract => contract.Status).HasConversion<string>().IsRequired();
        builder.Property(contract => contract.SignedAt).IsRequired();

        builder.HasOne(contract => contract.Sponsor)
            .WithMany()
            .HasForeignKey(contract => contract.SponsorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(contract => contract.TeamId);
        builder.HasIndex(contract => new { contract.TeamId, contract.Status });
    }
}
