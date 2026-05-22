using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rappix.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "orders");

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiveCount = table.Column<int>(type: "integer", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "order_states",
                schema: "orders",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DeliveryLatitude = table.Column<double>(type: "double precision", nullable: false),
                    DeliveryLongitude = table.Column<double>(type: "double precision", nullable: false),
                    PaymentCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourierId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompensationTerminal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MerchantTimeoutTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentTimeoutTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourierTimeoutTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_states", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vertical = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Tip = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    delivery_street = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    delivery_reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    delivery_latitude = table.Column<double>(type: "double precision", nullable: false),
                    delivery_longitude = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InProgressAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "orders",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    ModifierTotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    LineSubtotal = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_lines_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "orders",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnqueueTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "orders",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "orders",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "orders",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_order_id",
                schema: "orders",
                table: "order_lines",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerUserId",
                schema: "orders",
                table: "orders",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_MerchantId_Status",
                schema: "orders",
                table: "orders",
                columns: new[] { "MerchantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status",
                schema: "orders",
                table: "orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "orders",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "orders",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "orders",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "orders",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "orders",
                table: "OutboxState",
                column: "Created");

            // Esquema estandar de Quartz.NET 3.x para PostgreSQL (job store ADO del scheduler de la saga).
            // Tablas qrtz_* en minusculas (el prefijo "QRTZ_" sin comillas que usa Quartz se pliega a minusculas
            // en Postgres). Se crean aqui para que migrate-on-startup provisione el scheduler con el resto del esquema.
            migrationBuilder.Sql(@"
CREATE TABLE qrtz_job_details (
  sched_name TEXT NOT NULL, job_name TEXT NOT NULL, job_group TEXT NOT NULL, description TEXT NULL,
  job_class_name TEXT NOT NULL, is_durable BOOL NOT NULL, is_nonconcurrent BOOL NOT NULL,
  is_update_data BOOL NOT NULL, requests_recovery BOOL NOT NULL, job_data BYTEA NULL,
  PRIMARY KEY (sched_name, job_name, job_group));
CREATE TABLE qrtz_triggers (
  sched_name TEXT NOT NULL, trigger_name TEXT NOT NULL, trigger_group TEXT NOT NULL, job_name TEXT NOT NULL,
  job_group TEXT NOT NULL, description TEXT NULL, next_fire_time BIGINT NULL, prev_fire_time BIGINT NULL,
  priority INTEGER NULL, trigger_state TEXT NOT NULL, trigger_type TEXT NOT NULL, start_time BIGINT NOT NULL,
  end_time BIGINT NULL, calendar_name TEXT NULL, misfire_instr SMALLINT NULL, job_data BYTEA NULL,
  PRIMARY KEY (sched_name, trigger_name, trigger_group),
  FOREIGN KEY (sched_name, job_name, job_group) REFERENCES qrtz_job_details(sched_name, job_name, job_group));
CREATE TABLE qrtz_simple_triggers (
  sched_name TEXT NOT NULL, trigger_name TEXT NOT NULL, trigger_group TEXT NOT NULL, repeat_count BIGINT NOT NULL,
  repeat_interval BIGINT NOT NULL, times_triggered BIGINT NOT NULL,
  PRIMARY KEY (sched_name, trigger_name, trigger_group),
  FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE);
CREATE TABLE qrtz_simprop_triggers (
  sched_name TEXT NOT NULL, trigger_name TEXT NOT NULL, trigger_group TEXT NOT NULL, str_prop_1 TEXT NULL,
  str_prop_2 TEXT NULL, str_prop_3 TEXT NULL, int_prop_1 INT NULL, int_prop_2 INT NULL, long_prop_1 BIGINT NULL,
  long_prop_2 BIGINT NULL, dec_prop_1 NUMERIC NULL, dec_prop_2 NUMERIC NULL, bool_prop_1 BOOL NULL,
  bool_prop_2 BOOL NULL, time_zone_id TEXT NULL,
  PRIMARY KEY (sched_name, trigger_name, trigger_group),
  FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE);
CREATE TABLE qrtz_cron_triggers (
  sched_name TEXT NOT NULL, trigger_name TEXT NOT NULL, trigger_group TEXT NOT NULL, cron_expression TEXT NOT NULL,
  time_zone_id TEXT NULL,
  PRIMARY KEY (sched_name, trigger_name, trigger_group),
  FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE);
CREATE TABLE qrtz_blob_triggers (
  sched_name TEXT NOT NULL, trigger_name TEXT NOT NULL, trigger_group TEXT NOT NULL, blob_data BYTEA NULL,
  PRIMARY KEY (sched_name, trigger_name, trigger_group),
  FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE);
CREATE TABLE qrtz_calendars (
  sched_name TEXT NOT NULL, calendar_name TEXT NOT NULL, calendar BYTEA NOT NULL,
  PRIMARY KEY (sched_name, calendar_name));
CREATE TABLE qrtz_paused_trigger_grps (
  sched_name TEXT NOT NULL, trigger_group TEXT NOT NULL,
  PRIMARY KEY (sched_name, trigger_group));
CREATE TABLE qrtz_fired_triggers (
  sched_name TEXT NOT NULL, entry_id TEXT NOT NULL, trigger_name TEXT NOT NULL, trigger_group TEXT NOT NULL,
  instance_name TEXT NOT NULL, fired_time BIGINT NOT NULL, sched_time BIGINT NOT NULL, priority INTEGER NOT NULL,
  state TEXT NOT NULL, job_name TEXT NULL, job_group TEXT NULL, is_nonconcurrent BOOL NULL, requests_recovery BOOL NULL,
  PRIMARY KEY (sched_name, entry_id));
CREATE TABLE qrtz_scheduler_state (
  sched_name TEXT NOT NULL, instance_name TEXT NOT NULL, last_checkin_time BIGINT NOT NULL, checkin_interval BIGINT NOT NULL,
  PRIMARY KEY (sched_name, instance_name));
CREATE TABLE qrtz_locks (
  sched_name TEXT NOT NULL, lock_name TEXT NOT NULL,
  PRIMARY KEY (sched_name, lock_name));
CREATE INDEX idx_qrtz_t_next_fire_time ON qrtz_triggers(sched_name, next_fire_time);
CREATE INDEX idx_qrtz_t_state ON qrtz_triggers(sched_name, trigger_state);
CREATE INDEX idx_qrtz_t_nft_st ON qrtz_triggers(sched_name, next_fire_time, trigger_state);
CREATE INDEX idx_qrtz_ft_trig_inst_name ON qrtz_fired_triggers(sched_name, instance_name);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_lines",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "order_states",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "orders");

            // Tablas del scheduler Quartz.
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS qrtz_fired_triggers, qrtz_paused_trigger_grps, qrtz_scheduler_state, qrtz_locks,
  qrtz_simple_triggers, qrtz_simprop_triggers, qrtz_cron_triggers, qrtz_blob_triggers, qrtz_triggers,
  qrtz_job_details, qrtz_calendars CASCADE;
");
        }
    }
}
