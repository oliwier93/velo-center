using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeloCenter.Infrastructure.Persistence;

#nullable disable

namespace VeloCenter.Infrastructure.Migrations;

[DbContext(typeof(VeloCenterDbContext))]
[Migration("20260414093000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "activities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Source = table.Column<int>(type: "INTEGER", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                StartTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                DistanceKm = table.Column<double>(type: "REAL", nullable: false),
                DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_activities", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_activities_StartTime",
            table: "activities",
            column: "StartTime");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "activities");
    }
}
