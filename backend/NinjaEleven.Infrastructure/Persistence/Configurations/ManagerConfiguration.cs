using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Managers;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class ManagerConfiguration : IEntityTypeConfiguration<Manager>
{
    public void Configure(EntityTypeBuilder<Manager> builder)
    {
        builder.ToTable("managers");

        builder.HasKey(manager => manager.Id);
        builder.Property(manager => manager.Id).ValueGeneratedNever();

         builder.Property(manager => manager.Name).HasMaxLength(120).IsRequired();
        builder.Property(manager => manager.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(manager => manager.UserId).HasColumnName("user_id");
        builder.Property(manager => manager.StartedAt).HasColumnName("started_at").IsRequired();

        builder.HasIndex(manager => manager.TeamId).IsUnique();
    }
}
