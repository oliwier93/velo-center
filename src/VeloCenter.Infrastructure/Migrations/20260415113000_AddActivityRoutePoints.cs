using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeloCenter.Infrastructure.Persistence;

#nullable disable

namespace VeloCenter.Infrastructure.Migrations;

[DbContext(typeof(VeloCenterDbContext))]
[Migration("20260415113000_AddActivityRoutePoints")]
public partial class AddActivityRoutePoints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "activity_route_points",
            columns: table => new
            {
                ActivityId = table.Column<Guid>(type: "TEXT", nullable: false),
                Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                Latitude = table.Column<double>(type: "REAL", nullable: false),
                Longitude = table.Column<double>(type: "REAL", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_activity_route_points", x => new { x.ActivityId, x.Sequence });
                table.ForeignKey(
                    name: "FK_activity_route_points_activities_ActivityId",
                    column: x => x.ActivityId,
                    principalTable: "activities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_activity_route_points_ActivityId",
            table: "activity_route_points",
            column: "ActivityId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "activity_route_points");
    }
}
