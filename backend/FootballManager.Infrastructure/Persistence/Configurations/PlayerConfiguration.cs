using FootballManager.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(120).IsRequired();
        builder.Property(p => p.BirthDate).HasColumnType("date").IsRequired();
        builder.Property(p => p.Position)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(p => p.Speed).IsRequired();
        builder.Property(p => p.Accuracy).IsRequired();
        builder.Property(p => p.Dribbling).IsRequired();
        builder.Property(p => p.Heading).IsRequired();
        builder.Property(p => p.Strength).IsRequired();
        builder.Property(p => p.GoalkeeperPower).IsRequired();
        builder.Property(p => p.Reflexes).IsRequired();

        builder.HasIndex(p => p.Name);
    }
}
