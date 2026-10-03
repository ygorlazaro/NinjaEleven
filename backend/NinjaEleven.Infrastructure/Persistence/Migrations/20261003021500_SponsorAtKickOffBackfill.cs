using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NinjaEleven.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Stamps the company each club was carrying onto the matches it had already played.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stamp is written at the kick-off, and a world whose shirt system arrived halfway
    /// through a season has three thousand matches whose two companies were never written down.
    /// Every one of them is a result page and a report that shows a bare shirt for a club that
    /// was being paid by somebody.
    /// </para>
    /// <para>
    /// So the past is derived here, once, exactly the way a club's page derives its promotions:
    /// the deal that had been signed when the whistle went is a comparison over the contracts a
    /// club already had, and it is compared with the row that is being stamped. The newest deal
    /// signed at or before the match is the one that club's shirt carried, and where there is
    /// no such deal the column is left empty — a club that had not signed anything at that
    /// point is not given a company to fill the gap.
    /// </para>
    /// <para>
    /// It runs once and writes only where the column is still null, so a match stamped at its
    /// own kick-off keeps the sponsor it actually had, and running the migration again writes
    /// nothing.
    /// </para>
    /// </remarks>
    public partial class SponsorAtKickOffBackfill : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE matches AS m
                SET home_sponsor_id = worn.sponsor_id
                FROM (
                    SELECT DISTINCT ON (contract.team_id, match.id)
                        contract.team_id,
                        match.id AS match_id,
                        contract.sponsor_id
                    FROM sponsor_contracts AS contract
                    JOIN matches AS match
                        ON match.home_team_id = contract.team_id
                       AND contract.signed_at <= match.created_at
                    WHERE match.home_sponsor_id IS NULL
                    ORDER BY contract.team_id, match.id, contract.signed_at DESC
                ) AS worn
                WHERE worn.match_id = m.id
                  AND worn.team_id = m.home_team_id;

                UPDATE matches AS m
                SET away_sponsor_id = worn.sponsor_id
                FROM (
                    SELECT DISTINCT ON (contract.team_id, match.id)
                        contract.team_id,
                        match.id AS match_id,
                        contract.sponsor_id
                    FROM sponsor_contracts AS contract
                    JOIN matches AS match
                        ON match.away_team_id = contract.team_id
                       AND contract.signed_at <= match.created_at
                    WHERE match.away_sponsor_id IS NULL
                    ORDER BY contract.team_id, match.id, contract.signed_at DESC
                ) AS worn
                WHERE worn.match_id = m.id
                  AND worn.team_id = m.away_team_id;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The two columns came from the migration before this one and are left alone: this
            // one only fills in what that one left empty, and rolling it back must not take a
            // stamp written at a real kick-off with it.
        }
    }
}