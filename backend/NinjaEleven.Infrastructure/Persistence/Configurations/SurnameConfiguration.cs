using NinjaEleven.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class SurnameConfiguration : IEntityTypeConfiguration<Surname>
{
    public void Configure(EntityTypeBuilder<Surname> builder)
    {
        builder.ToTable("surnames");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Value).HasMaxLength(120).IsRequired();
        builder.HasIndex(s => s.Value).IsUnique();
    }
}
