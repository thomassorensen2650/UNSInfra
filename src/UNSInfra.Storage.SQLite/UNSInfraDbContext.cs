using Microsoft.EntityFrameworkCore;
using UNSInfra.Storage.SQLite.Entities;

namespace UNSInfra.Storage.SQLite;

/// <summary>
/// Entity Framework DbContext for UNS Infrastructure SQLite database.
/// </summary>
public class UNSInfraDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the UNSInfraDbContext class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public UNSInfraDbContext(DbContextOptions<UNSInfraDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the hierarchy configurations.
    /// </summary>
    public DbSet<HierarchyConfigurationEntity> HierarchyConfigurations { get; set; }

    /// <summary>
    /// Gets or sets the hierarchy nodes.
    /// </summary>
    public DbSet<HierarchyNodeEntity> HierarchyNodes { get; set; }

    /// <summary>
    /// Gets or sets the topic configurations.
    /// </summary>
    public DbSet<TopicConfigurationEntity> TopicConfigurations { get; set; }

    /// <summary>
    /// Gets or sets the data schemas.
    /// </summary>
    public DbSet<DataSchemaEntity> DataSchemas { get; set; }

    /// <summary>
    /// Gets or sets the data points for historical storage.
    /// </summary>
    public DbSet<DataPointEntity> DataPoints { get; set; }

    /// <summary>
    /// Gets or sets the namespace configurations.
    /// </summary>
    public DbSet<NamespaceConfigurationEntity> NamespaceConfigurations { get; set; }
    
    /// <summary>
    /// Gets or sets the NS tree instances.
    /// </summary>
    public DbSet<NSTreeInstanceEntity> NSTreeInstances { get; set; }

    /// <summary>
    /// Gets or sets the data ingestion configurations.
    /// </summary>
    public DbSet<DataIngestionConfigurationEntity> DataIngestionConfigurations { get; set; }

    /// <summary>
    /// Gets or sets the input/output configurations.
    /// </summary>
    public DbSet<InputOutputConfigurationEntity> InputOutputConfigurations { get; set; }

    /// <summary>
    /// Configures the model relationships and constraints.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure HierarchyConfiguration
        modelBuilder.Entity<HierarchyConfigurationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Name);
        });

        // Configure HierarchyNode
        modelBuilder.Entity<HierarchyNodeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.HierarchyConfigurationId, e.Order });
            
            // Self-referencing relationship for parent-child
            entity.HasOne(e => e.ParentNode)
                  .WithMany(e => e.ChildNodes)
                  .HasForeignKey(e => e.ParentNodeId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Relationship to HierarchyConfiguration
            entity.HasOne(e => e.HierarchyConfiguration)
                  .WithMany(e => e.Nodes)
                  .HasForeignKey(e => e.HierarchyConfigurationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure TopicConfiguration
        modelBuilder.Entity<TopicConfigurationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Topic).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SourceType);
            entity.HasIndex(e => e.NamespaceConfigurationId);
        });

        // Configure DataSchema
        modelBuilder.Entity<DataSchemaEntity>(entity =>
        {
            entity.HasKey(e => e.SchemaId);
            entity.HasIndex(e => e.Topic);
        });

        // Configure DataPoint
        modelBuilder.Entity<DataPointEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Topic);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.Topic, e.Timestamp });
            entity.HasIndex(e => e.Source);
        });

        // Configure NamespaceConfiguration
        modelBuilder.Entity<NamespaceConfigurationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.ParentNamespaceId);
            entity.HasIndex(e => new { e.Name, e.HierarchicalPathJson });
        });

        // Configure NSTreeInstance
        modelBuilder.Entity<NSTreeInstanceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.HierarchyNodeId);
            entity.HasIndex(e => e.ParentInstanceId);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure DataIngestionConfiguration
        modelBuilder.Entity<DataIngestionConfigurationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.ServiceType);
            entity.HasIndex(e => e.Enabled);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ModifiedAt);
        });

        // Configure InputOutputConfiguration
        modelBuilder.Entity<InputOutputConfigurationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.ServiceType);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ModifiedAt);
        });
    }
}