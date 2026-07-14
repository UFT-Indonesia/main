using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendancePolicyHistoryForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_AttendancePolicyHistories_AttendancePolicies_policy_id",
                table: "AttendancePolicyHistories",
                column: "policy_id",
                principalTable: "AttendancePolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendancePolicyHistories_AttendancePolicies_policy_id",
                table: "AttendancePolicyHistories");
        }
    }
}
