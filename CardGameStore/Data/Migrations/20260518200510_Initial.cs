using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    image_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    link_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    type = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_admin_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    actor_user_id = table.Column<string>(type: "TEXT", nullable: true),
                    actor_user_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    details = table.Column<string>(type: "TEXT", nullable: true),
                    ip_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "championships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    game = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    start_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    registration_deadline = table.Column<DateTime>(type: "TEXT", nullable: true),
                    max_participants = table.Column<int>(type: "INTEGER", nullable: true),
                    entry_fee_in_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_admin_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_championships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    price_in_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    stock_quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    minimum_stock = table.Column<int>(type: "INTEGER", nullable: false),
                    image_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_featured = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "TEXT", nullable: true),
                    whatsapp = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    cpf = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    refresh_token = table.Column<string>(type: "TEXT", nullable: true),
                    refresh_token_expiry = table.Column<DateTime>(type: "TEXT", nullable: true),
                    password_reset_token = table.Column<string>(type: "TEXT", nullable: true),
                    password_reset_token_expiry = table.Column<DateTime>(type: "TEXT", nullable: true),
                    points_balance = table.Column<int>(type: "INTEGER", nullable: false),
                    points_expires_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    consent_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comandas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    table_identifier = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    opened_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    closed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    championship_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    total_in_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    points_applied = table.Column<int>(type: "INTEGER", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comandas", x => x.id);
                    table.ForeignKey(
                        name: "FK_comandas_championships_championship_id",
                        column: x => x.championship_id,
                        principalTable: "championships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_comandas_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cookie_consents",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    ip_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    accepted = table.Column<bool>(type: "INTEGER", nullable: false),
                    policy_version = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    consent_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cookie_consents", x => x.id);
                    table.ForeignKey(
                        name: "FK_cookie_consents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "lgpd_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    requester_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    requester_email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    requester_cpf = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    request_type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    admin_response = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    responded_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deadline = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lgpd_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_lgpd_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "championship_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    championship_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    player_number = table.Column<int>(type: "INTEGER", nullable: false),
                    deck_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    placement = table.Column<int>(type: "INTEGER", nullable: true),
                    registered_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    comanda_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_championship_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_championship_participants_championships_championship_id",
                        column: x => x.championship_id,
                        principalTable: "championships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_championship_participants_comandas_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comandas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_championship_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comanda_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    comanda_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    card_cache_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    item_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    unit_price_in_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    subtotal_in_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    added_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    added_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comanda_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_comanda_items_comandas_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comandas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comanda_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "crediarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    comanda_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    valor_em_centavos = table.Column<int>(type: "INTEGER", nullable: false),
                    data_abertura = table.Column<DateTime>(type: "TEXT", nullable: false),
                    data_vencimento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    data_pagamento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    observacao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    aberto_por_admin_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pago_por_admin_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crediarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_crediarios_comandas_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comandas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crediarios_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "consent_at", "cpf", "created_at", "deleted_at", "email", "is_active", "name", "password_hash", "password_reset_token", "password_reset_token_expiry", "points_balance", "points_expires_at", "refresh_token", "refresh_token_expiry", "role", "updated_at", "whatsapp" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "admin@cardgamestore.com.br", true, "Maikon", "$2b$12$2FtwJhHP3JiQZjXb.f19B.ulmS5t2jdIQbKAPhPYP22AT.0ptsVPC", null, null, 0, null, null, null, "Admin", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_championship_participants_comanda_id",
                table: "championship_participants",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_championship_participants_unique",
                table: "championship_participants",
                columns: new[] { "championship_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_championship_participants_user_id",
                table: "championship_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_championships_start_date",
                table: "championships",
                column: "start_date");

            migrationBuilder.CreateIndex(
                name: "ix_championships_status",
                table: "championships",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_comanda_items_comanda",
                table: "comanda_items",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "IX_comanda_items_product_id",
                table: "comanda_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_comandas_championship",
                table: "comandas",
                column: "championship_id");

            migrationBuilder.CreateIndex(
                name: "ix_comandas_status",
                table: "comandas",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_comandas_user_status",
                table: "comandas",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_cookie_consents_consent_at",
                table: "cookie_consents",
                column: "consent_at");

            migrationBuilder.CreateIndex(
                name: "IX_cookie_consents_user_id",
                table: "cookie_consents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_crediarios_comanda_id",
                table: "crediarios",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_crediarios_status",
                table: "crediarios",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_crediarios_user_status",
                table: "crediarios",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_lgpd_requests_email",
                table: "lgpd_requests",
                column: "requester_email");

            migrationBuilder.CreateIndex(
                name: "ix_lgpd_requests_status",
                table: "lgpd_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_lgpd_requests_user_id",
                table: "lgpd_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_category",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_products_is_active",
                table: "products",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_users_cpf",
                table: "users",
                column: "cpf",
                unique: true,
                filter: "cpf IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_whatsapp",
                table: "users",
                column: "whatsapp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "championship_participants");

            migrationBuilder.DropTable(
                name: "comanda_items");

            migrationBuilder.DropTable(
                name: "cookie_consents");

            migrationBuilder.DropTable(
                name: "crediarios");

            migrationBuilder.DropTable(
                name: "lgpd_requests");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "comandas");

            migrationBuilder.DropTable(
                name: "championships");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
