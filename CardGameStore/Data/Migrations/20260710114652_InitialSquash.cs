using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardGameStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSquash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    link_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    actor_user_id = table.Column<string>(type: "text", nullable: true),
                    actor_user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    details = table.Column<string>(type: "text", nullable: true),
                    ip_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "external_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nfe_key = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fiscal_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: false),
                    razao_social = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    inscricao_estadual = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    logradouro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    codigo_municipio_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    cep = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    csc_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    csc_token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    regime_tributario = table.Column<string>(type: "text", nullable: false),
                    ambiente = table.Column<string>(type: "text", nullable: false),
                    serie_nfce = table.Column<int>(type: "integer", nullable: false),
                    proximo_numero_nfce = table.Column<int>(type: "integer", nullable: false),
                    email_contador = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    certificado_pfx_encrypted = table.Column<string>(type: "text", nullable: true),
                    certificado_senha_encrypted = table.Column<string>(type: "text", nullable: true),
                    certificado_validade = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    certificado_uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    certificado_ultimo_alerta_limiar = table.Column<int>(type: "integer", nullable: true),
                    ultimo_envio_mensal_xmls = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dist_ultimo_nsu = table.Column<long>(type: "bigint", nullable: false),
                    formas_pagamento_auto_emissao = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_token = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    refresh_token = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    client_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    client_secret = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    cnpj = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: true),
                    pix_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "naturezas_operacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cfop = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    csosn = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    percentual_credito_sn = table.Column<decimal>(type: "numeric", nullable: true),
                    is_padrao = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_naturezas_operacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notas_destinadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_acesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    nsu = table.Column<long>(type: "bigint", nullable: false),
                    emitente_cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    emitente_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    valor = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    data_emissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ciencia_protocolo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ciencia_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xml_proc = table.Column<string>(type: "text", nullable: true),
                    contas_geradas = table.Column<int>(type: "integer", nullable: false),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notas_destinadas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notas_fiscais_emitidas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origem = table.Column<string>(type: "text", nullable: false),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: true),
                    venda_avulsa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    valor_total_em_centavos = table.Column<int>(type: "integer", nullable: false),
                    serie = table.Column<int>(type: "integer", nullable: true),
                    numero = table.Column<int>(type: "integer", nullable: true),
                    chave_acesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    protocolo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    motivo_rejeicao = table.Column<string>(type: "text", nullable: true),
                    xml_autorizado = table.Column<string>(type: "text", nullable: true),
                    url_qrcode = table.Column<string>(type: "text", nullable: true),
                    emitido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    justificativa_cancelamento = table.Column<string>(type: "text", nullable: true),
                    inutilizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    protocolo_inutilizacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tentativas_reprocessamento = table.Column<int>(type: "integer", nullable: false),
                    cnf_contingencia = table.Column<int>(type: "integer", nullable: true),
                    dh_contingencia = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    justificativa_contingencia = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notas_fiscais_emitidas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perfis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    permissoes_json = table.Column<string>(type: "text", nullable: false),
                    criado_por_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perfis", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    emoji = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "site_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    hero_subtitle = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    address_line = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contact_person_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    whatsapp_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    logo_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    nav_produtos_label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nav_pontos_label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    cta_ver_produtos_label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    produtos_eyebrow = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    produtos_title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    pontos_eyebrow = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    pontos_title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    pontos_paragraph = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    color_primary = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    color_accent = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    color_navy = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    color_background = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    color_card = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "timers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    paused_remaining = table.Column<int>(type: "integer", nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sound_preset = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    warn_at_seconds = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vendas_avulsas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    items_json = table.Column<string>(type: "jsonb", nullable: false),
                    total_in_cents = table.Column<int>(type: "integer", nullable: false),
                    payment_method = table.Column<string>(type: "text", nullable: false),
                    second_payment_method = table.Column<string>(type: "text", nullable: true),
                    second_payment_amount_in_cents = table.Column<int>(type: "integer", nullable: false),
                    client_name = table.Column<string>(type: "text", nullable: true),
                    sold_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sold_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sold_by_admin_name = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "text", nullable: true),
                    discount_percent = table.Column<int>(type: "integer", nullable: false),
                    discount_in_cents = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendas_avulsas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cost_price_in_cents = table.Column<int>(type: "integer", nullable: false),
                    price_in_cents = table.Column<int>(type: "integer", nullable: false),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false),
                    minimum_stock = table.Column<int>(type: "integer", nullable: false),
                    ncm = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    natureza_operacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_urls = table.Column<string[]>(type: "text[]", nullable: false),
                    full_description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_featured = table.Column<bool>(type: "boolean", nullable: false),
                    show_on_site = table.Column<bool>(type: "boolean", nullable: false),
                    show_on_marketplace = table.Column<bool>(type: "boolean", nullable: false),
                    discount_price_in_cents = table.Column<int>(type: "integer", nullable: true),
                    is_pre_venda = table.Column<bool>(type: "boolean", nullable: false),
                    has_variants = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_naturezas_operacao_natureza_operacao_id",
                        column: x => x.natureza_operacao_id,
                        principalTable: "naturezas_operacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    whatsapp = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    profile_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    refresh_token = table.Column<string>(type: "text", nullable: true),
                    refresh_token_expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    password_reset_token = table.Column<string>(type: "text", nullable: true),
                    password_reset_token_expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    points_balance = table.Column<int>(type: "integer", nullable: false),
                    points_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    balance_in_cents = table.Column<int>(type: "integer", nullable: false),
                    preferences_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    color = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false),
                    price_in_cents = table.Column<int>(type: "integer", nullable: true),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comandas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_identifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    second_payment_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    second_payment_amount_in_cents = table.Column<int>(type: "integer", nullable: false),
                    total_in_cents = table.Column<int>(type: "integer", nullable: false),
                    points_applied = table.Column<int>(type: "integer", nullable: false),
                    discount_in_cents = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comandas", x => x.id);
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
                    id = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    policy_version = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    consent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    id = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requester_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requester_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    requester_cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    request_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    admin_response = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    anexo_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    anexo_dados = table.Column<byte[]>(type: "bytea", nullable: true)
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
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_waitlist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    whatsapp = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_waitlist", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_waitlist_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_waitlist_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "push_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    p256dh = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    auth = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_push_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reserved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fulfilled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_reservations", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_reservations_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_product_reservations_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_reservations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comanda_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_price_in_cents = table.Column<int>(type: "integer", nullable: false),
                    cost_price_snapshot_in_cents = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    subtotal_in_cents = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    added_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valor_em_centavos = table.Column<int>(type: "integer", nullable: false),
                    valor_pago_em_centavos = table.Column<int>(type: "integer", nullable: false),
                    data_abertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_vencimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_pagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aberto_por_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pago_por_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    itens_json = table.Column<string>(type: "text", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "pagamentos_crediario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    crediario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_em_centavos = table.Column<int>(type: "integer", nullable: false),
                    forma_pagamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentos_crediario", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagamentos_crediario_crediarios_crediario_id",
                        column: x => x.crediario_id,
                        principalTable: "crediarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pix_cobrancas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origem = table.Column<string>(type: "text", nullable: false),
                    crediario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: true),
                    venda_avulsa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tx_id = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    valor_em_centavos = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pix_copia_cola = table.Column<string>(type: "text", nullable: true),
                    imagem_qrcode = table.Column<string>(type: "text", nullable: true),
                    nome_devedor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_por_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pago_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pix_cobrancas", x => x.id);
                    table.ForeignKey(
                        name: "FK_pix_cobrancas_comandas_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comandas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pix_cobrancas_crediarios_crediario_id",
                        column: x => x.crediario_id,
                        principalTable: "crediarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "ix_comanda_items_comanda",
                table: "comanda_items",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "IX_comanda_items_product_id",
                table: "comanda_items",
                column: "product_id");

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
                name: "ix_ext_tx_source_external_id",
                table: "external_transactions",
                columns: new[] { "source", "external_id" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_fiscal_config_cnpj",
                table: "fiscal_config",
                column: "cnpj");

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
                name: "ix_naturezas_operacao_descricao",
                table: "naturezas_operacao",
                column: "descricao");

            migrationBuilder.CreateIndex(
                name: "ix_naturezas_operacao_unica_padrao",
                table: "naturezas_operacao",
                column: "is_padrao",
                unique: true,
                filter: "is_padrao = true");

            migrationBuilder.CreateIndex(
                name: "ix_notas_destinadas_chave",
                table: "notas_destinadas",
                column: "chave_acesso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notas_destinadas_status",
                table: "notas_destinadas",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_chave_acesso",
                table: "notas_fiscais_emitidas",
                column: "chave_acesso",
                unique: true,
                filter: "chave_acesso IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_comanda",
                table: "notas_fiscais_emitidas",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_emitido_em",
                table: "notas_fiscais_emitidas",
                column: "emitido_em");

            migrationBuilder.CreateIndex(
                name: "ix_notas_fiscais_status",
                table: "notas_fiscais_emitidas",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_crediario_created_at",
                table: "pagamentos_crediario",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_crediario_crediario",
                table: "pagamentos_crediario",
                column: "crediario_id");

            migrationBuilder.CreateIndex(
                name: "ix_perfis_nome",
                table: "perfis",
                column: "nome");

            migrationBuilder.CreateIndex(
                name: "ix_pix_cobrancas_comanda",
                table: "pix_cobrancas",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_pix_cobrancas_crediario",
                table: "pix_cobrancas",
                column: "crediario_id");

            migrationBuilder.CreateIndex(
                name: "ix_pix_cobrancas_tx_id",
                table: "pix_cobrancas",
                column: "tx_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_name",
                table: "product_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_reservations_product",
                table: "product_reservations",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_reservations_status",
                table: "product_reservations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_product_reservations_user",
                table: "product_reservations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_reservations_variant_id",
                table: "product_reservations",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_product",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_waitlist_product",
                table: "product_waitlist",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_waitlist_user",
                table: "product_waitlist",
                column: "user_id",
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_products_barcode",
                table: "products",
                column: "barcode",
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_products_category",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_products_is_active",
                table: "products",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_products_natureza_operacao",
                table: "products",
                column: "natureza_operacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_endpoint",
                table: "push_subscriptions",
                column: "endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_push_subscriptions_user_id",
                table: "push_subscriptions",
                column: "user_id");

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
                name: "IX_users_perfil_id",
                table: "users",
                column: "perfil_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_whatsapp",
                table: "users",
                column: "whatsapp");

            migrationBuilder.CreateIndex(
                name: "ix_vendas_avulsas_sold_at",
                table: "vendas_avulsas",
                column: "sold_at");

            migrationBuilder.CreateIndex(
                name: "ix_vendas_avulsas_user_id",
                table: "vendas_avulsas",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "comanda_items");

            migrationBuilder.DropTable(
                name: "cookie_consents");

            migrationBuilder.DropTable(
                name: "external_transactions");

            migrationBuilder.DropTable(
                name: "fiscal_config");

            migrationBuilder.DropTable(
                name: "integration_configs");

            migrationBuilder.DropTable(
                name: "lgpd_requests");

            migrationBuilder.DropTable(
                name: "notas_destinadas");

            migrationBuilder.DropTable(
                name: "notas_fiscais_emitidas");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "pagamentos_crediario");

            migrationBuilder.DropTable(
                name: "pix_cobrancas");

            migrationBuilder.DropTable(
                name: "product_categories");

            migrationBuilder.DropTable(
                name: "product_reservations");

            migrationBuilder.DropTable(
                name: "product_waitlist");

            migrationBuilder.DropTable(
                name: "push_subscriptions");

            migrationBuilder.DropTable(
                name: "site_config");

            migrationBuilder.DropTable(
                name: "timers");

            migrationBuilder.DropTable(
                name: "vendas_avulsas");

            migrationBuilder.DropTable(
                name: "crediarios");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "comandas");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "naturezas_operacao");

            migrationBuilder.DropTable(
                name: "perfis");
        }
    }
}
