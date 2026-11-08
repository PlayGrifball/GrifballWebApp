using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class StatsSheetsAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraint would refuse first: a stint below 1 (the import always counted from 1, so none
            // is expected). Each becomes the player's next stint of that season's phase, after their last one from 1 (one
            // below 1 alone becomes the first), so none clashes with a row already there (the unique index): 0 is the next
            // stint, -1 the one after, and so on. The totals stay.
            migrationBuilder.Sql("UPDATE \"PlayerSeasonStats\" SET \"StintIndex\" = (SELECT COALESCE(MAX(o.\"StintIndex\"), 0) FROM \"PlayerSeasonStats\" o WHERE o.\"SeasonID\" = \"PlayerSeasonStats\".\"SeasonID\" AND o.\"UserID\" = \"PlayerSeasonStats\".\"UserID\" AND o.\"IsPlayoffs\" = \"PlayerSeasonStats\".\"IsPlayoffs\" AND o.\"StintIndex\" >= 1) + 1 - \"StintIndex\" WHERE \"StintIndex\" < 1;");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PlayerSeasonStats_History_Update";
                DROP TRIGGER IF EXISTS "PlayerSeasonStats_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_PlayerSeasonStats_StintFromOne",
                schema: "Event",
                table: "PlayerSeasonStats",
                sql: "StintIndex >= 1");

            migrationBuilder.Sql("""
                CREATE TRIGGER "PlayerSeasonStats_History_Update" AFTER UPDATE ON "PlayerSeasonStats" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerSeasonStatsHistory" ("Assists", "AverageLifeSeconds", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "ExtendedMetricsJson", "Exterminations", "GameVersionID", "Gamertag", "GamesPlayed", "Goals", "HammerKills", "IsCombined", "IsPlayoffs", "KdRatio", "KdSpread", "Kills", "Losses", "ModifiedAt", "ModifiedByID", "MultiKills", "PeriodStart", "PlayerSeasonStatID", "Punches", "RoleNote", "Score", "SeasonID", "Sprees", "StintIndex", "SwordKills", "TeamID", "TotalSecondsPlayed", "TradeNote", "UserID", "WinPercent", "Wins", "PeriodEnd") VALUES (OLD."Assists", OLD."AverageLifeSeconds", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."ExtendedMetricsJson", OLD."Exterminations", OLD."GameVersionID", OLD."Gamertag", OLD."GamesPlayed", OLD."Goals", OLD."HammerKills", OLD."IsCombined", OLD."IsPlayoffs", OLD."KdRatio", OLD."KdSpread", OLD."Kills", OLD."Losses", OLD."ModifiedAt", OLD."ModifiedByID", OLD."MultiKills", OLD."PeriodStart", OLD."PlayerSeasonStatID", OLD."Punches", OLD."RoleNote", OLD."Score", OLD."SeasonID", OLD."Sprees", OLD."StintIndex", OLD."SwordKills", OLD."TeamID", OLD."TotalSecondsPlayed", OLD."TradeNote", OLD."UserID", OLD."WinPercent", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PlayerSeasonStats" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PlayerSeasonStats_History_Delete" AFTER DELETE ON "PlayerSeasonStats" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerSeasonStatsHistory" ("Assists", "AverageLifeSeconds", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "ExtendedMetricsJson", "Exterminations", "GameVersionID", "Gamertag", "GamesPlayed", "Goals", "HammerKills", "IsCombined", "IsPlayoffs", "KdRatio", "KdSpread", "Kills", "Losses", "ModifiedAt", "ModifiedByID", "MultiKills", "PeriodStart", "PlayerSeasonStatID", "Punches", "RoleNote", "Score", "SeasonID", "Sprees", "StintIndex", "SwordKills", "TeamID", "TotalSecondsPlayed", "TradeNote", "UserID", "WinPercent", "Wins", "PeriodEnd") VALUES (OLD."Assists", OLD."AverageLifeSeconds", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."ExtendedMetricsJson", OLD."Exterminations", OLD."GameVersionID", OLD."Gamertag", OLD."GamesPlayed", OLD."Goals", OLD."HammerKills", OLD."IsCombined", OLD."IsPlayoffs", OLD."KdRatio", OLD."KdSpread", OLD."Kills", OLD."Losses", OLD."ModifiedAt", OLD."ModifiedByID", OLD."MultiKills", OLD."PeriodStart", OLD."PlayerSeasonStatID", OLD."Punches", OLD."RoleNote", OLD."Score", OLD."SeasonID", OLD."Sprees", OLD."StintIndex", OLD."SwordKills", OLD."TeamID", OLD."TotalSecondsPlayed", OLD."TradeNote", OLD."UserID", OLD."WinPercent", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A stint the constraint settled keeps its new number: it is the player's next stint
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PlayerSeasonStats_History_Update";
                DROP TRIGGER IF EXISTS "PlayerSeasonStats_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_PlayerSeasonStats_StintFromOne",
                schema: "Event",
                table: "PlayerSeasonStats");

            migrationBuilder.Sql("""
                CREATE TRIGGER "PlayerSeasonStats_History_Update" AFTER UPDATE ON "PlayerSeasonStats" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerSeasonStatsHistory" ("Assists", "AverageLifeSeconds", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "ExtendedMetricsJson", "Exterminations", "GameVersionID", "Gamertag", "GamesPlayed", "Goals", "HammerKills", "IsCombined", "IsPlayoffs", "KdRatio", "KdSpread", "Kills", "Losses", "ModifiedAt", "ModifiedByID", "MultiKills", "PeriodStart", "PlayerSeasonStatID", "Punches", "RoleNote", "Score", "SeasonID", "Sprees", "StintIndex", "SwordKills", "TeamID", "TotalSecondsPlayed", "TradeNote", "UserID", "WinPercent", "Wins", "PeriodEnd") VALUES (OLD."Assists", OLD."AverageLifeSeconds", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."ExtendedMetricsJson", OLD."Exterminations", OLD."GameVersionID", OLD."Gamertag", OLD."GamesPlayed", OLD."Goals", OLD."HammerKills", OLD."IsCombined", OLD."IsPlayoffs", OLD."KdRatio", OLD."KdSpread", OLD."Kills", OLD."Losses", OLD."ModifiedAt", OLD."ModifiedByID", OLD."MultiKills", OLD."PeriodStart", OLD."PlayerSeasonStatID", OLD."Punches", OLD."RoleNote", OLD."Score", OLD."SeasonID", OLD."Sprees", OLD."StintIndex", OLD."SwordKills", OLD."TeamID", OLD."TotalSecondsPlayed", OLD."TradeNote", OLD."UserID", OLD."WinPercent", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PlayerSeasonStats" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PlayerSeasonStats_History_Delete" AFTER DELETE ON "PlayerSeasonStats" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerSeasonStatsHistory" ("Assists", "AverageLifeSeconds", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "ExtendedMetricsJson", "Exterminations", "GameVersionID", "Gamertag", "GamesPlayed", "Goals", "HammerKills", "IsCombined", "IsPlayoffs", "KdRatio", "KdSpread", "Kills", "Losses", "ModifiedAt", "ModifiedByID", "MultiKills", "PeriodStart", "PlayerSeasonStatID", "Punches", "RoleNote", "Score", "SeasonID", "Sprees", "StintIndex", "SwordKills", "TeamID", "TotalSecondsPlayed", "TradeNote", "UserID", "WinPercent", "Wins", "PeriodEnd") VALUES (OLD."Assists", OLD."AverageLifeSeconds", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."ExtendedMetricsJson", OLD."Exterminations", OLD."GameVersionID", OLD."Gamertag", OLD."GamesPlayed", OLD."Goals", OLD."HammerKills", OLD."IsCombined", OLD."IsPlayoffs", OLD."KdRatio", OLD."KdSpread", OLD."Kills", OLD."Losses", OLD."ModifiedAt", OLD."ModifiedByID", OLD."MultiKills", OLD."PeriodStart", OLD."PlayerSeasonStatID", OLD."Punches", OLD."RoleNote", OLD."Score", OLD."SeasonID", OLD."Sprees", OLD."StintIndex", OLD."SwordKills", OLD."TeamID", OLD."TotalSecondsPlayed", OLD."TradeNote", OLD."UserID", OLD."WinPercent", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
