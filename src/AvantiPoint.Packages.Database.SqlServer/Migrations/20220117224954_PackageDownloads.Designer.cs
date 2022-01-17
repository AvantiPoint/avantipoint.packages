﻿// <auto-generated />
using System;
using AvantiPoint.Packages.Database.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    [DbContext(typeof(SqlServerContext))]
    [Migration("20220117224954_PackageDownloads")]
    partial class PackageDownloads
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("AvantiPoint.Packages.Core.Package", b =>
                {
                    b.Property<int>("Key")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Key"), 1L, 1);

                    b.Property<string>("Authors")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<string>("Description")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<long>("Downloads")
                        .HasColumnType("bigint");

                    b.Property<bool>("HasEmbeddedIcon")
                        .HasColumnType("bit");

                    b.Property<bool>("HasReadme")
                        .HasColumnType("bit");

                    b.Property<string>("IconUrl")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<string>("Id")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<bool>("IsDevelopmentDependency")
                        .HasColumnType("bit");

                    b.Property<bool>("IsPrerelease")
                        .HasColumnType("bit");

                    b.Property<bool>("IsSigned")
                        .HasColumnType("bit");

                    b.Property<bool>("IsTool")
                        .HasColumnType("bit");

                    b.Property<string>("Language")
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<string>("LicenseExpression")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("LicenseUrl")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<bool>("Listed")
                        .HasColumnType("bit");

                    b.Property<string>("MinClientVersion")
                        .HasMaxLength(44)
                        .HasColumnType("nvarchar(44)");

                    b.Property<string>("NormalizedVersionString")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)")
                        .HasColumnName("Version");

                    b.Property<string>("OriginalVersionString")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)")
                        .HasColumnName("OriginalVersion");

                    b.Property<string>("ProjectUrl")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<DateTime>("Published")
                        .HasColumnType("datetime2");

                    b.Property<string>("ReleaseNotes")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)")
                        .HasColumnName("ReleaseNotes");

                    b.Property<string>("RepositoryType")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("RepositoryUrl")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<bool>("RequireLicenseAcceptance")
                        .HasColumnType("bit");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<int>("SemVerLevel")
                        .HasColumnType("int");

                    b.Property<string>("Summary")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<string>("Tags")
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<string>("Title")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Key");

                    b.HasIndex("Id");

                    b.HasIndex("Id", "NormalizedVersionString")
                        .IsUnique();

                    b.ToTable("Packages");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.PackageDependency", b =>
                {
                    b.Property<int>("Key")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Key"), 1L, 1);

                    b.Property<string>("Id")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<int>("PackageKey")
                        .HasColumnType("int");

                    b.Property<string>("TargetFramework")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("VersionRange")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Key");

                    b.HasIndex("Id");

                    b.HasIndex("PackageKey");

                    b.ToTable("PackageDependencies");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.PackageDownload", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ClientPlatform")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("ClientPlatformVersion")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("NuGetClient")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("NuGetClientVersion")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<int>("PackageKey")
                        .HasColumnType("int");

                    b.Property<string>("RemoteIp")
                        .HasColumnType("nvarchar(45)");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("User")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserAgentString")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("PackageKey");

                    b.ToTable("PackageDownloads");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.PackageType", b =>
                {
                    b.Property<int>("Key")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Key"), 1L, 1);

                    b.Property<string>("Name")
                        .HasMaxLength(512)
                        .HasColumnType("nvarchar(512)");

                    b.Property<int>("PackageKey")
                        .HasColumnType("int");

                    b.Property<string>("Version")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.HasKey("Key");

                    b.HasIndex("Name");

                    b.HasIndex("PackageKey");

                    b.ToTable("PackageTypes");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.TargetFramework", b =>
                {
                    b.Property<int>("Key")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Key"), 1L, 1);

                    b.Property<string>("Moniker")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<int>("PackageKey")
                        .HasColumnType("int");

                    b.HasKey("Key");

                    b.HasIndex("Moniker");

                    b.HasIndex("PackageKey");

                    b.ToTable("TargetFrameworks");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.PackageDependency", b =>
                {
                    b.HasOne("AvantiPoint.Packages.Core.Package", "Package")
                        .WithMany("Dependencies")
                        .HasForeignKey("PackageKey")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Package");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.PackageDownload", b =>
                {
                    b.HasOne("AvantiPoint.Packages.Core.Package", "Package")
                        .WithMany("PackageDownloads")
                        .HasForeignKey("PackageKey")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Package");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.PackageType", b =>
                {
                    b.HasOne("AvantiPoint.Packages.Core.Package", "Package")
                        .WithMany("PackageTypes")
                        .HasForeignKey("PackageKey")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Package");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.TargetFramework", b =>
                {
                    b.HasOne("AvantiPoint.Packages.Core.Package", "Package")
                        .WithMany("TargetFrameworks")
                        .HasForeignKey("PackageKey")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Package");
                });

            modelBuilder.Entity("AvantiPoint.Packages.Core.Package", b =>
                {
                    b.Navigation("Dependencies");

                    b.Navigation("PackageDownloads");

                    b.Navigation("PackageTypes");

                    b.Navigation("TargetFrameworks");
                });
#pragma warning restore 612, 618
        }
    }
}
