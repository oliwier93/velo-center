using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeloCenter.Infrastructure.Persistence;

#nullable disable

namespace VeloCenter.Infrastructure.Migrations;

[DbContext(typeof(VeloCenterDbContext))]
[Migration("20260414094000_AddActivityImportMetadata")]
public partial class AddActivityImportMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ImportFingerprint",
            table: "activities",
            type: "TEXT",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ImportedAt",
            table: "activities",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastUpdatedAt",
            table: "activities",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceActivityId",
            table: "activities",
            type: "TEXT",
            maxLength: 128,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_activities_Source_ImportFingerprint",
            table: "activities",
            columns: new[] { "Source", "ImportFingerprint" },
            unique: true,
            filter: "\"ImportFingerprint\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_activities_Source_SourceActivityId",
            table: "activities",
            columns: new[] { "Source", "SourceActivityId" },
            unique: true,
            filter: "\"SourceActivityId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_activities_Source_ImportFingerprint",
            table: "activities");

        migrationBuilder.DropIndex(
            name: "IX_activities_Source_SourceActivityId",
            table: "activities");

        migrationBuilder.DropColumn(
            name: "ImportFingerprint",
            table: "activities");

        migrationBuilder.DropColumn(
            name: "ImportedAt",
            table: "activities");

        migrationBuilder.DropColumn(
            name: "LastUpdatedAt",
            table: "activities");

        migrationBuilder.DropColumn(
            name: "SourceActivityId",
            table: "activities");
    }
}
