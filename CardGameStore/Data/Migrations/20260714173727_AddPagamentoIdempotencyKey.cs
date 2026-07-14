using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPagamentoIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "idempotency_key",
                table: "pagamentos_crediario",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_crediario_idempotency_key",
                table: "pagamentos_crediario",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pagamentos_crediario_idempotency_key",
                table: "pagamentos_crediario");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "pagamentos_crediario");
        }
    }
}
