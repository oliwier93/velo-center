using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VeloCenter.Infrastructure.Persistence;

#nullable disable

namespace VeloCenter.Infrastructure.Migrations;

[DbContext(typeof(VeloCenterDbContext))]
partial class VeloCenterDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("VeloCenter.Infrastructure.Persistence.ActivityRecord", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("TEXT");

                b.Property<double>("DistanceKm")
                    .HasColumnType("REAL");

                b.Property<int>("DurationSeconds")
                    .HasColumnType("INTEGER");

                b.Property<string>("ImportFingerprint")
                    .HasMaxLength(128)
                    .HasColumnType("TEXT");

                b.Property<DateTimeOffset?>("ImportedAt")
                    .HasColumnType("TEXT");

                b.Property<DateTimeOffset?>("LastUpdatedAt")
                    .HasColumnType("TEXT");

                b.Property<int>("Source")
                    .HasColumnType("INTEGER");

                b.Property<string>("SourceActivityId")
                    .HasMaxLength(128)
                    .HasColumnType("TEXT");

                b.Property<DateTimeOffset>("StartTime")
                    .HasColumnType("TEXT");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("StartTime");

                b.HasIndex("Source", "ImportFingerprint")
                    .IsUnique()
                    .HasFilter("\"ImportFingerprint\" IS NOT NULL");

                b.HasIndex("Source", "SourceActivityId")
                    .IsUnique()
                    .HasFilter("\"SourceActivityId\" IS NOT NULL");

                b.ToTable("activities", (string)null);
            });
#pragma warning restore 612, 618
    }
}
