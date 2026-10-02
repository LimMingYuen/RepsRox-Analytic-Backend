using Microsoft.EntityFrameworkCore;

namespace RepsRox.Analytics.Api.Data;

public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<SheetImport> Imports => Set<SheetImport>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<TrainingSet> TrainingSets => Set<TrainingSet>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<Race> Races => Set<Race>();
    public DbSet<RaceLeg> RaceLegs => Set<RaceLeg>();
    public DbSet<WeighIn> WeighIns => Set<WeighIn>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<SheetImport>(e =>
        {
            e.ToTable("Imports");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.HasIndex(x => new { x.Kind, x.Month }).IsUnique();
        });

        model.Entity<TrainingSession>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasOne(x => x.Import).WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Date);
        });

        model.Entity<TrainingSet>(e =>
        {
            e.Property(x => x.Exercise).HasMaxLength(200);
            e.Property(x => x.Unit).HasConversion<string>().HasMaxLength(8);
            e.Property(x => x.WeightKg).HasPrecision(7, 2);
            e.HasOne(x => x.Session).WithMany(s => s.Sets).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Exercise);
        });

        model.Entity<Meal>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Detail).HasMaxLength(1000);
            e.HasOne(x => x.Import).WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Date);
        });

        model.Entity<Race>(e =>
        {
            e.HasOne(x => x.Import).WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Date);
        });

        model.Entity<RaceLeg>(e =>
        {
            e.Property(x => x.Tag).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasOne(x => x.Race).WithMany(r => r.Legs).HasForeignKey(x => x.RaceId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<WeighIn>(e =>
        {
            e.Property(x => x.WeightKg).HasPrecision(5, 1);
            e.HasOne(x => x.Import).WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Date);
        });
    }
}
