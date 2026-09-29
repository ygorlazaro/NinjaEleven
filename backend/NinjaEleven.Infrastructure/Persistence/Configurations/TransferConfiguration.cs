using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Players;
using NinjaEleven.Domain.Seasons;
using NinjaEleven.Domain.Teams;
using NinjaEleven.Domain.Transfers;

namespace NinjaEleven.Infrastructure.Configurations;

/// <summary>
/// Maps a transfer proposal to the <c>transfers</c> table.
/// </summary>
public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");

        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.HasOne(t => t.Player)
            .WithMany()
            .HasForeignKey(t => t.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // The seller is optional because a player with no club can be signed, and a signing is
        // a transfer whose seller is nobody. The buyer is never optional: a transfer without one
        // is not a transfer.
        builder.HasOne(t => t.SellingClub)
            .WithMany()
            .HasForeignKey(t => t.SellingClubId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.BuyingClub)
            .WithMany()
            .HasForeignKey(t => t.BuyingClubId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(t => t.ProposalSeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        // Also optional, and for the same kind of reason: a deal agreed in a season that is
        // still being played is waiting for a season the world has not opened yet. The number
        // it arrives in is not optional, and that is what the market is settled on.
        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(t => t.ArrivalSeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .ValueGeneratedNever();

        builder.Property(t => t.Fee)
            .HasPrecision(14, 2)
            .ValueGeneratedNever();

        builder.Property(t => t.ProposedAt)
            .ValueGeneratedNever();

        // The column keeps the name it was born with: it holds a round number and has held one
        // since the first migration, and renaming a column to tell the truth about it is a
        // migration spent on a word rather than on a rule.
        builder.Property(t => t.ArrivalRoundNumber)
            .HasColumnName("arrival_round_id")
            .ValueGeneratedNever();

        // The round the proposal was made in and the round by which the selling club must
        // answer. The deadline is null for a club with a manager, which answers on its own
        // schedule; it is set for a club without one, which has to be told when to decide.
        builder.Property(t => t.ProposalRoundNumber)
            .HasColumnName("proposal_round_number")
            .ValueGeneratedNever();

        builder.Property(t => t.AnswerByRound)
            .HasColumnName("answer_by_round")
            .ValueGeneratedNever();

        builder.HasIndex(t => new { t.SellingClubId, t.BuyingClubId, t.PlayerId });

        builder.HasIndex(t => new { t.ProposalSeasonId, t.Status });

        // The completion sweep asks for every accepted deal due in a season by the season's
        // number, which is the one field it knows before the season row exists.
        builder.HasIndex(t => new { t.ArrivalSeasonNumber, t.Status });
    }
}