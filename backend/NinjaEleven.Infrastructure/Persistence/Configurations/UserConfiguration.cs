using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Managers;
using NinjaEleven.Domain.Users;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(120).IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.DismissedAt).HasColumnName("dismissed_at");
        builder.Property(u => u.DismissedTeamId).HasColumnName("dismissed_team_id");

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasOne(u => u.Manager)
            .WithOne(m => m.User)
            .HasForeignKey<Manager>(m => m.UserId)
            .IsRequired(false);
    }
}
