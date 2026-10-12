using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Entities;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<SchoolProfile> SchoolProfiles => Set<SchoolProfile>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Section> Sections => Set<Section>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasCharSet("utf8mb4");

        modelBuilder.Entity<SchoolProfile>(entity =>
        {
            entity.ToTable("SchoolProfiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Name).HasMaxLength(160).IsRequired();
            entity.Property(profile => profile.Address).HasMaxLength(500);
            entity.Property(profile => profile.Phone).HasMaxLength(40);
            entity.Property(profile => profile.Email).HasMaxLength(254);
            entity.Property(profile => profile.TimeZoneId).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.UserId).HasMaxLength(450);
            entity.Property(profile => profile.FullName).HasMaxLength(160).IsRequired();
            entity.Property(profile => profile.Phone).HasMaxLength(40);
            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<UserProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AcademicYear>(entity =>
        {
            entity.ToTable("AcademicYears");
            entity.HasKey(year => year.Id);
            entity.Property(year => year.Name).HasMaxLength(32).IsRequired();
            entity.Property(year => year.StartDate).HasColumnType("date");
            entity.Property(year => year.EndDate).HasColumnType("date");
            entity.HasIndex(year => year.Name).IsUnique();
        });

        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("Classes");
            entity.HasKey(schoolClass => schoolClass.Id);
            entity.Property(schoolClass => schoolClass.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(schoolClass => schoolClass.Name).IsUnique();
            entity.HasIndex(schoolClass => schoolClass.GradeLevel).IsUnique();
            entity.HasMany(schoolClass => schoolClass.Sections)
                .WithOne(section => section.Class)
                .HasForeignKey(section => section.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Section>(entity =>
        {
            entity.ToTable("Sections");
            entity.HasKey(section => section.Id);
            entity.Property(section => section.Name).HasMaxLength(40).IsRequired();
            entity.HasIndex(section => new { section.ClassId, section.Name }).IsUnique();
        });
    }
}
