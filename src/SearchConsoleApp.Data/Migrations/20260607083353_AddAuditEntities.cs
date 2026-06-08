using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SearchConsoleApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditIssue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    PageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RuleId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FixHint = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DocUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditIssue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActorCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorUserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ActorDeviceId = table.Column<long>(type: "bigint", nullable: true),
                    ActorSessionId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "success"),
                    FailureReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogArchive",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalId = table.Column<long>(type: "bigint", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActorCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorUserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ActorDeviceId = table.Column<long>(type: "bigint", nullable: true),
                    ActorSessionId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    ArchivedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogArchive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditRun",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InputUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    NormalizedUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PagesCrawled = table.Column<int>(type: "int", nullable: false),
                    IssuesFound = table.Column<int>(type: "int", nullable: false),
                    CriticalCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    InfoCount = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customer",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    Roles = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, defaultValue: "user"),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TotpSecret = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RecoveryCodesHashes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Device",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FirstUserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Trusted = table.Column<bool>(type: "bit", nullable: false),
                    BiometricEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Device", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceSession",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceId = table.Column<long>(type: "bigint", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpCountry = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    IpCity = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActivityUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RevokedByCustomerId = table.Column<long>(type: "bigint", nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSession", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceToken",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceToken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalLogin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LinkedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLogin", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Audience = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScannedPage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditRunId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CrawlDepth = table.Column<int>(type: "int", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "int", nullable: true),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannedPage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityToken",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityToken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Theme",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    JsonContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Theme", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditIssue_AuditRunId",
                table: "AuditIssue",
                column: "AuditRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditIssue_AuditRunId_Severity",
                table: "AuditIssue",
                columns: new[] { "AuditRunId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditIssue_EntityId",
                table: "AuditIssue",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditIssue_RuleId",
                table: "AuditIssue",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Action",
                table: "AuditLog",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ActorCustomerId",
                table: "AuditLog",
                column: "ActorCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CorrelationId",
                table: "AuditLog",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_EntityId",
                table: "AuditLog",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TargetType_TargetId",
                table: "AuditLog",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp",
                table: "AuditLog",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_Action",
                table: "AuditLogArchive",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_ActorCustomerId",
                table: "AuditLogArchive",
                column: "ActorCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_OriginalId",
                table: "AuditLogArchive",
                column: "OriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_TargetType_TargetId",
                table: "AuditLogArchive",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogArchive_Timestamp",
                table: "AuditLogArchive",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRun_CreatedAt",
                table: "AuditRun",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRun_EntityId",
                table: "AuditRun",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditRun_NormalizedUrl",
                table: "AuditRun",
                column: "NormalizedUrl");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRun_Status",
                table: "AuditRun",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_Email",
                table: "Customer",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customer_EntityId",
                table: "Customer",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Device_CustomerId",
                table: "Device",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Device_CustomerId_Fingerprint",
                table: "Device",
                columns: new[] { "CustomerId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Device_EntityId",
                table: "Device",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSession_CustomerId",
                table: "DeviceSession",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSession_DeviceId",
                table: "DeviceSession",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSession_EntityId",
                table: "DeviceSession",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSession_RefreshTokenHash",
                table: "DeviceSession",
                column: "RefreshTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSession_StartedUtc",
                table: "DeviceSession",
                column: "StartedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceToken_CustomerId_Token",
                table: "DeviceToken",
                columns: new[] { "CustomerId", "Token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceToken_EntityId",
                table: "DeviceToken",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogin_CustomerId",
                table: "ExternalLogin",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogin_EntityId",
                table: "ExternalLogin",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogin_Provider_ProviderUserId",
                table: "ExternalLogin",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessage_EntityId",
                table: "InboxMessage",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessage_Source_ExternalEventId",
                table: "InboxMessage",
                columns: new[] { "Source", "ExternalEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessage_Status",
                table: "InboxMessage",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_CreatedOnUtc",
                table: "OutboxMessage",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EntityId",
                table: "OutboxMessage",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Status_AvailableAtUtc",
                table: "OutboxMessage",
                columns: new[] { "Status", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_CustomerId",
                table: "RefreshToken",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_EntityId",
                table: "RefreshToken",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_TokenHash",
                table: "RefreshToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScannedPage_AuditRunId",
                table: "ScannedPage",
                column: "AuditRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ScannedPage_AuditRunId_Url",
                table: "ScannedPage",
                columns: new[] { "AuditRunId", "Url" });

            migrationBuilder.CreateIndex(
                name: "IX_ScannedPage_EntityId",
                table: "ScannedPage",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityToken_CustomerId_Purpose",
                table: "SecurityToken",
                columns: new[] { "CustomerId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityToken_EntityId",
                table: "SecurityToken",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityToken_TokenHash",
                table: "SecurityToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Theme_EntityId",
                table: "Theme",
                column: "EntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Theme_Name",
                table: "Theme",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditIssue");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "AuditLogArchive");

            migrationBuilder.DropTable(
                name: "AuditRun");

            migrationBuilder.DropTable(
                name: "Customer");

            migrationBuilder.DropTable(
                name: "Device");

            migrationBuilder.DropTable(
                name: "DeviceSession");

            migrationBuilder.DropTable(
                name: "DeviceToken");

            migrationBuilder.DropTable(
                name: "ExternalLogin");

            migrationBuilder.DropTable(
                name: "InboxMessage");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropTable(
                name: "ScannedPage");

            migrationBuilder.DropTable(
                name: "SecurityToken");

            migrationBuilder.DropTable(
                name: "Theme");
        }
    }
}
