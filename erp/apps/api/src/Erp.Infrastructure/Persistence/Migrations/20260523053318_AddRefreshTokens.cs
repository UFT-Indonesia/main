using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_by_user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthRefreshTokens_AuthUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AuthUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthRefreshTokens_expires_at_utc",
                table: "AuthRefreshTokens",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_AuthRefreshTokens_family_id",
                table: "AuthRefreshTokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_AuthRefreshTokens_token_hash",
                table: "AuthRefreshTokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthRefreshTokens_user_id",
                table: "AuthRefreshTokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthRefreshTokens");
        }
    }
