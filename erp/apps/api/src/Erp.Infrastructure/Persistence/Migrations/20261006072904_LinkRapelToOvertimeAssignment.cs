using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class LinkRapelToOvertimeAssignment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "assignment_id",
            table: "Rapels",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<TimeOnly>(
            name: "claimed_end",
            table: "Rapels",
            type: "time",
            nullable: false,
            defaultValue: new TimeOnly(0, 0, 0));

        migrationBuilder.AddColumn<int>(
            name: "claimed_hours",
            table: "Rapels",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "claimed_start",
            table: "Rapels",
            type: "time",
            nullable: false,
            defaultValue: new TimeOnly(0, 0, 0));

        migrationBuilder.AddColumn<decimal>(
            name: "suggested_amount",
            table: "Rapels",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateIndex(
            name: "IX_Rapels_assignment_id",
            table: "Rapels",
            column: "assignment_id",
            unique: true,
            filter: "status IN ('Pending', 'Approved')");

        migrationBuilder.AddForeignKey(
            name: "FK_Rapels_OvertimeAssignments_assignment_id",
            table: "Rapels",
            column: "assignment_id",
            principalTable: "OvertimeAssignments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Rapels_OvertimeAssignments_assignment_id",
            table: "Rapels");

        migrationBuilder.DropIndex(
            name: "IX_Rapels_assignment_id",
            table: "Rapels");

        migrationBuilder.DropColumn(
            name: "assignment_id",
            table: "Rapels");

        migrationBuilder.DropColumn(
            name: "claimed_end",
            table: "Rapels");

        migrationBuilder.DropColumn(
            name: "claimed_hours",
            table: "Rapels");

        migrationBuilder.DropColumn(
            name: "claimed_start",
            table: "Rapels");

        migrationBuilder.DropColumn(
            name: "suggested_amount",
            table: "Rapels");
    }
}
