using NinjaEleven.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

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

        // A faces.js `FaceConfig` as raw JSON, and deliberately untyped on this side: the
        // shape is the library's, and a C# class mirroring it would be a second copy of it to
        // keep in step. The column holds a string, so the value travels to the client as the
        // JSON it already is and the client draws it with the library that drew it. It is
        // nullable because `''` is not a JSON document: a player with no face is null.
        builder.Property(p => p.Face).HasColumnType("jsonb");

        builder.HasIndex(p => p.Name);
    }
}
