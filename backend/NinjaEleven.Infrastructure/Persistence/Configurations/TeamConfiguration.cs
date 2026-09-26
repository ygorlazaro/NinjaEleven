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

        builder.HasIndex(t => t.Name);
    }
}
