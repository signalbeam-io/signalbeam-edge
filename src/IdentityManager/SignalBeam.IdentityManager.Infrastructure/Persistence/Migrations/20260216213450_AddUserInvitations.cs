using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalBeam.IdentityManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_invitations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_invitations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_invitations_email",
                schema: "identity",
                table: "user_invitations",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_user_invitations_status",
                schema: "identity",
                table: "user_invitations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_user_invitations_tenant_id",
                schema: "identity",
                table: "user_invitations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_invitations_token",
                schema: "identity",
                table: "user_invitations",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_invitations",
                schema: "identity");
        }
    }
}
