using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UNSInfra.Storage.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectionConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ConnectionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoStart = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConnectionConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    InputsJson = table.Column<string>(type: "TEXT", nullable: false),
                    OutputsJson = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataIngestionConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ServiceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataIngestionConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataPoints",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PathValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ValueJson = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quality = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSchemas",
                columns: table => new
                {
                    SchemaId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    JsonSchema = table.Column<string>(type: "TEXT", nullable: false),
                    PropertyTypesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationRulesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSchemas", x => x.SchemaId);
                });

            migrationBuilder.CreateTable(
                name: "HierarchyConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystemDefined = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HierarchyConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InputOutputConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ServiceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ConnectionId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InputOutputConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NamespaceConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    HierarchicalPathJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParentNamespaceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AllowedParentHierarchyNodeId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamespaceConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NSTreeInstances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HierarchyNodeId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ParentInstanceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NSTreeInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TopicConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PathValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    NamespaceConfigurationId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    NSPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UNSName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HierarchyNodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ParentNodeId = table.Column<string>(type: "TEXT", nullable: true),
                    HierarchyConfigurationId = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    AllowedChildNodeIdsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HierarchyNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HierarchyNodes_HierarchyConfigurations_HierarchyConfigurationId",
                        column: x => x.HierarchyConfigurationId,
                        principalTable: "HierarchyConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HierarchyNodes_HierarchyNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalTable: "HierarchyNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionConfigurations_AutoStart",
                table: "ConnectionConfigurations",
                column: "AutoStart");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionConfigurations_ConnectionType",
                table: "ConnectionConfigurations",
                column: "ConnectionType");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionConfigurations_CreatedAt",
                table: "ConnectionConfigurations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionConfigurations_IsEnabled",
                table: "ConnectionConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionConfigurations_ModifiedAt",
                table: "ConnectionConfigurations",
                column: "ModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionConfigurations_Name",
                table: "ConnectionConfigurations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DataIngestionConfigurations_CreatedAt",
                table: "DataIngestionConfigurations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DataIngestionConfigurations_Enabled",
                table: "DataIngestionConfigurations",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_DataIngestionConfigurations_ModifiedAt",
                table: "DataIngestionConfigurations",
                column: "ModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DataIngestionConfigurations_Name",
                table: "DataIngestionConfigurations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DataIngestionConfigurations_ServiceType",
                table: "DataIngestionConfigurations",
                column: "ServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_DataPoints_Source",
                table: "DataPoints",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_DataPoints_Timestamp",
                table: "DataPoints",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_DataPoints_Topic",
                table: "DataPoints",
                column: "Topic");

            migrationBuilder.CreateIndex(
                name: "IX_DataPoints_Topic_Timestamp",
                table: "DataPoints",
                columns: new[] { "Topic", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DataSchemas_Topic",
                table: "DataSchemas",
                column: "Topic");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyConfigurations_IsActive",
                table: "HierarchyConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyConfigurations_Name",
                table: "HierarchyConfigurations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodes_HierarchyConfigurationId_Order",
                table: "HierarchyNodes",
                columns: new[] { "HierarchyConfigurationId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodes_ParentNodeId",
                table: "HierarchyNodes",
                column: "ParentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_InputOutputConfigurations_ConnectionId",
                table: "InputOutputConfigurations",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InputOutputConfigurations_CreatedAt",
                table: "InputOutputConfigurations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InputOutputConfigurations_IsEnabled",
                table: "InputOutputConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_InputOutputConfigurations_ModifiedAt",
                table: "InputOutputConfigurations",
                column: "ModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InputOutputConfigurations_Name",
                table: "InputOutputConfigurations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_InputOutputConfigurations_ServiceType",
                table: "InputOutputConfigurations",
                column: "ServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_InputOutputConfigurations_Type",
                table: "InputOutputConfigurations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_NamespaceConfigurations_IsActive",
                table: "NamespaceConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_NamespaceConfigurations_Name",
                table: "NamespaceConfigurations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NamespaceConfigurations_Name_HierarchicalPathJson",
                table: "NamespaceConfigurations",
                columns: new[] { "Name", "HierarchicalPathJson" });

            migrationBuilder.CreateIndex(
                name: "IX_NamespaceConfigurations_ParentNamespaceId",
                table: "NamespaceConfigurations",
                column: "ParentNamespaceId");

            migrationBuilder.CreateIndex(
                name: "IX_NamespaceConfigurations_Type",
                table: "NamespaceConfigurations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_NSTreeInstances_HierarchyNodeId",
                table: "NSTreeInstances",
                column: "HierarchyNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NSTreeInstances_IsActive",
                table: "NSTreeInstances",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_NSTreeInstances_Name",
                table: "NSTreeInstances",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NSTreeInstances_ParentInstanceId",
                table: "NSTreeInstances",
                column: "ParentInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicConfigurations_IsActive",
                table: "TopicConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TopicConfigurations_NamespaceConfigurationId",
                table: "TopicConfigurations",
                column: "NamespaceConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicConfigurations_SourceType",
                table: "TopicConfigurations",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_TopicConfigurations_Topic",
                table: "TopicConfigurations",
                column: "Topic",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionConfigurations");

            migrationBuilder.DropTable(
                name: "DataIngestionConfigurations");

            migrationBuilder.DropTable(
                name: "DataPoints");

            migrationBuilder.DropTable(
                name: "DataSchemas");

            migrationBuilder.DropTable(
                name: "HierarchyNodes");

            migrationBuilder.DropTable(
                name: "InputOutputConfigurations");

            migrationBuilder.DropTable(
                name: "NamespaceConfigurations");

            migrationBuilder.DropTable(
                name: "NSTreeInstances");

            migrationBuilder.DropTable(
                name: "TopicConfigurations");

            migrationBuilder.DropTable(
                name: "HierarchyConfigurations");
        }
    }
}
