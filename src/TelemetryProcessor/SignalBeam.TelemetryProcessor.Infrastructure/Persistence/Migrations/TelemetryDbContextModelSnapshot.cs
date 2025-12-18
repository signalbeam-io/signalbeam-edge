using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SignalBeam.TelemetryProcessor.Infrastructure.Persistence;

#nullable disable

namespace SignalBeam.TelemetryProcessor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TelemetryDbContext))]
partial class TelemetryDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasDefaultSchema("telemetry_processor")
            .HasAnnotation("ProductVersion", "9.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("SignalBeam.Domain.Entities.DeviceHeartbeat", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTimeOffset>("Timestamp")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("timestamp");

                b.Property<string>("AdditionalData")
                    .HasColumnType("jsonb")
                    .HasColumnName("additional_data");

                b.Property<Guid>("DeviceId")
                    .HasColumnType("uuid")
                    .HasColumnName("device_id");

                b.Property<string>("IpAddress")
                    .HasMaxLength(45)
                    .HasColumnType("character varying(45)")
                    .HasColumnName("ip_address");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("character varying(50)")
                    .HasColumnName("status");

                b.HasKey("Id", "Timestamp");

                b.HasIndex("DeviceId")
                    .HasDatabaseName("ix_device_heartbeats_device_id");

                b.HasIndex("Timestamp")
                    .IsDescending()
                    .HasDatabaseName("ix_device_heartbeats_timestamp");

                b.HasIndex("DeviceId", "Timestamp")
                    .IsDescending(false, true)
                    .HasDatabaseName("ix_device_heartbeats_device_timestamp");

                b.ToTable("device_heartbeats", "telemetry_processor", t =>
                {
                    t.HasComment("TimescaleDB hypertable for device heartbeats");
                });
            });

        modelBuilder.Entity("SignalBeam.Domain.Entities.DeviceMetrics", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTimeOffset>("Timestamp")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("timestamp");

                b.Property<string>("AdditionalMetrics")
                    .HasColumnType("jsonb")
                    .HasColumnName("additional_metrics");

                b.Property<decimal>("CpuUsage")
                    .HasPrecision(5, 2)
                    .HasColumnType("numeric(5,2)")
                    .HasColumnName("cpu_usage");

                b.Property<Guid>("DeviceId")
                    .HasColumnType("uuid")
                    .HasColumnName("device_id");

                b.Property<decimal>("DiskUsage")
                    .HasPrecision(5, 2)
                    .HasColumnType("numeric(5,2)")
                    .HasColumnName("disk_usage");

                b.Property<decimal>("MemoryUsage")
                    .HasPrecision(5, 2)
                    .HasColumnType("numeric(5,2)")
                    .HasColumnName("memory_usage");

                b.Property<int>("RunningContainers")
                    .HasColumnType("integer")
                    .HasColumnName("running_containers");

                b.Property<long>("UptimeSeconds")
                    .HasColumnType("bigint")
                    .HasColumnName("uptime_seconds");

                b.HasKey("Id", "Timestamp");

                b.HasIndex("DeviceId")
                    .HasDatabaseName("ix_device_metrics_device_id");

                b.HasIndex("Timestamp")
                    .IsDescending()
                    .HasDatabaseName("ix_device_metrics_timestamp");

                b.HasIndex("DeviceId", "Timestamp")
                    .IsDescending(false, true)
                    .HasDatabaseName("ix_device_metrics_device_timestamp");

                b.ToTable("device_metrics", "telemetry_processor", t =>
                {
                    t.HasComment("TimescaleDB hypertable for device metrics");
                });
            });
#pragma warning restore 612, 618
    }
}
