using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Inbox;
using NinjaEleven.Domain.Teams;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        // Restrict, and not Cascade: a message is not a line of a club's book, it is a note
        // the book happened to produce. Removing the club removes the box that held it, which
        // is the club's business and not the note's.
        builder.HasOne<Team>().WithMany()
            .HasForeignKey(m => m.RecipientTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Category)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(m => m.Subject).HasMaxLength(200).IsRequired();
        builder.Property(m => m.SenderName).HasMaxLength(80).IsRequired();
        builder.Property(m => m.Body).IsRequired();

        // The names travel as one document, the same way a player's face does. The shape
        // belongs to whoever draws the screen, so the column stores it and nothing models it
        // a second time.
        builder.Property(m => m.Mentions)
            .HasColumnType("jsonb")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(m => m.LinkLabel).HasMaxLength(60);
        builder.Property(m => m.LinkRoute).HasMaxLength(200);

        builder.Property(m => m.Reference).HasMaxLength(160).IsRequired();

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.IsRead).IsRequired().HasDefaultValue(false);
        builder.Property(m => m.ReadAt);

        // The read is what stops a second message for the same thing from being written, and
        // the page is read club by club newest first: an inbox of two hundred lines is asked
        // for as "the last fifty of this club" and never as a question about the whole world.
        builder.HasIndex(m => new { m.RecipientTeamId, m.Reference }).IsUnique();
        builder.HasIndex(m => new { m.RecipientTeamId, m.CreatedAt });
        builder.HasIndex(m => new { m.RecipientTeamId, m.IsRead });
    }
}
