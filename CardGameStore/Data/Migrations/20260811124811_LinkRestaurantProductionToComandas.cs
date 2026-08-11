using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkRestaurantProductionToComandas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "restaurant_production_area_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "production_area_id",
                table: "comanda_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "production_area_name_snapshot",
                table: "comanda_items",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "production_ready_at",
                table: "comanda_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "production_served_at",
                table: "comanda_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "production_started_at",
                table: "comanda_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "production_status",
                table: "comanda_items",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_restaurant_production_area_id",
                table: "products",
                column: "restaurant_production_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_comanda_items_production_queue",
                table: "comanda_items",
                columns: new[] { "production_area_id", "production_status", "added_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_comanda_items_restaurant_production_areas_production_area_id",
                table: "comanda_items",
                column: "production_area_id",
                principalTable: "restaurant_production_areas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_restaurant_production_areas_restaurant_production_~",
                table: "products",
                column: "restaurant_production_area_id",
                principalTable: "restaurant_production_areas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comanda_items_restaurant_production_areas_production_area_id",
                table: "comanda_items");

            migrationBuilder.DropForeignKey(
                name: "FK_products_restaurant_production_areas_restaurant_production_~",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_restaurant_production_area_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_comanda_items_production_queue",
                table: "comanda_items");

            migrationBuilder.DropColumn(
                name: "restaurant_production_area_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "production_area_id",
                table: "comanda_items");

            migrationBuilder.DropColumn(
                name: "production_area_name_snapshot",
                table: "comanda_items");

            migrationBuilder.DropColumn(
                name: "production_ready_at",
                table: "comanda_items");

            migrationBuilder.DropColumn(
                name: "production_served_at",
                table: "comanda_items");

            migrationBuilder.DropColumn(
                name: "production_started_at",
                table: "comanda_items");

            migrationBuilder.DropColumn(
                name: "production_status",
                table: "comanda_items");
        }
    }
}
