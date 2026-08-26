using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TCS.Models;

namespace TCS.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<Trainee> Trainees => Set<Trainee>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<SessionRequest> SessionRequests => Set<SessionRequest>();
    public DbSet<TrainingConfirmation> TrainingConfirmations => Set<TrainingConfirmation>();
    public DbSet<ManagerApproval> ManagerApprovals => Set<ManagerApproval>();
    public DbSet<ApprovalPaper> ApprovalPapers => Set<ApprovalPaper>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<TransportAssignment> TransportAssignments => Set<TransportAssignment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // ---- UAT module ----
    public DbSet<CompanyBranch> CompanyBranches => Set<CompanyBranch>();
    public DbSet<UatProject> UatProjects => Set<UatProject>();
    public DbSet<UatSection> UatSections => Set<UatSection>();
    public DbSet<UatMasterItem> UatMasterItems => Set<UatMasterItem>();
    public DbSet<ProjectUatAttempt> ProjectUatAttempts => Set<ProjectUatAttempt>();
    public DbSet<ProjectUatResult> ProjectUatResults => Set<ProjectUatResult>();
    public DbSet<ProjectUatResultHistory> ProjectUatResultHistories => Set<ProjectUatResultHistory>();
    public DbSet<AcceptanceCert> AcceptanceCerts => Set<AcceptanceCert>();
    public DbSet<DigitalSignature> DigitalSignatures => Set<DigitalSignature>();
    public DbSet<CompanyStampAsset> CompanyStampAssets => Set<CompanyStampAsset>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Avoid multiple-cascade-path errors on SQLite/SQL Server by restricting
        // delete behavior on the optional / secondary FK relationships.

        builder.Entity<TrainingSession>()
            .HasOne(t => t.Company)
            .WithMany(c => c.TrainingSessions)
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TrainingSession>()
            .HasOne(t => t.Trainer)
            .WithMany(tr => tr.TrainingSessions)
            .HasForeignKey(t => t.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Trainee>()
            .HasOne(tr => tr.TrainingSession)
            .WithMany(s => s.Trainees)
            .HasForeignKey(tr => tr.TrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TrainingConfirmation>()
            .HasOne(c => c.TrainingSession)
            .WithOne(s => s.Confirmation)
            .HasForeignKey<TrainingConfirmation>(c => c.TrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TrainingConfirmation>()
            .HasOne(c => c.ManagerApproval)
            .WithMany(m => m.Confirmations)
            .HasForeignKey(c => c.ManagerApprovalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ApprovalPaper>()
            .HasOne(p => p.TrainingConfirmation)
            .WithOne(c => c.ApprovalPaper)
            .HasForeignKey<ApprovalPaper>(p => p.TrainingConfirmationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SessionRequest>()
            .HasOne(r => r.Company)
            .WithMany()
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TransportAssignment>()
            .HasOne(t => t.TrainingSession)
            .WithMany()
            .HasForeignKey(t => t.TrainingSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TransportAssignment>()
            .HasOne(t => t.Vehicle)
            .WithMany()
            .HasForeignKey(t => t.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Many SessionRequests can point at the same TransportAssignment (the
        // Transport Manager's same-site consolidation case).
        builder.Entity<SessionRequest>()
            .HasOne(r => r.TransportAssignment)
            .WithMany(t => t.SessionRequests)
            .HasForeignKey(r => r.TransportAssignmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Trainer)
            .WithMany()
            .HasForeignKey(u => u.TrainerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Company)
            .WithMany()
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TrainingConfirmation>()
            .Property(c => c.Status)
            .HasConversion<string>();
        builder.Entity<TrainingSession>()
            .Property(s => s.Status)
            .HasConversion<string>();
        builder.Entity<SessionRequest>()
            .Property(r => r.Status)
            .HasConversion<string>();
        builder.Entity<Trainee>()
            .Property(t => t.Attendance)
            .HasConversion<string>();
        builder.Entity<TransportAssignment>()
            .Property(t => t.Status)
            .HasConversion<string>();
        builder.Entity<TransportAssignment>()
            .Property(t => t.ApprovalStatus)
            .HasConversion<string>();
        builder.Entity<SessionRequest>()
            .Property(r => r.LocationType)
            .HasConversion<string>();

        // ---- UAT module relationships ----

        builder.Entity<CompanyBranch>()
            .HasOne(b => b.Company)
            .WithMany(c => c.Branches)
            .HasForeignKey(b => b.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UatProject>()
            .HasOne(p => p.CompanyBranch)
            .WithMany(b => b.Projects)
            .HasForeignKey(p => p.CompanyBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UatProject>()
            .HasOne(p => p.ProjectManager)
            .WithMany()
            .HasForeignKey(p => p.ProjectManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UatProject>()
            .HasOne(p => p.Consultant)
            .WithMany()
            .HasForeignKey(p => p.ConsultantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UatMasterItem>()
            .HasOne(m => m.Section)
            .WithMany(s => s.MasterItems)
            .HasForeignKey(m => m.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProjectUatAttempt>()
            .HasOne(a => a.UatProject)
            .WithMany(p => p.Attempts)
            .HasForeignKey(a => a.UatProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProjectUatAttempt>()
            .HasOne(a => a.InitiatedBy)
            .WithMany()
            .HasForeignKey(a => a.InitiatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProjectUatAttempt>()
            .HasOne(a => a.DecidedBy)
            .WithMany()
            .HasForeignKey(a => a.DecidedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProjectUatResult>()
            .HasOne(r => r.Attempt)
            .WithMany(a => a.Results)
            .HasForeignKey(r => r.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProjectUatResult>()
            .HasOne(r => r.MasterItem)
            .WithMany()
            .HasForeignKey(r => r.MasterItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProjectUatResult>()
            .HasOne(r => r.ExecutedBy)
            .WithMany()
            .HasForeignKey(r => r.ExecutedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProjectUatResult>()
            .HasOne(r => r.LastModifiedBy)
            .WithMany()
            .HasForeignKey(r => r.LastModifiedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProjectUatResultHistory>()
            .HasOne(h => h.Result)
            .WithMany(r => r.History)
            .HasForeignKey(h => h.ResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProjectUatResultHistory>()
            .HasOne(h => h.EditedBy)
            .WithMany()
            .HasForeignKey(h => h.EditedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AcceptanceCert>()
            .HasOne(c => c.Attempt)
            .WithOne(a => a.Certificate)
            .HasForeignKey<AcceptanceCert>(c => c.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DigitalSignature>()
            .HasOne(s => s.Attempt)
            .WithMany(a => a.Signatures)
            .HasForeignKey(s => s.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DigitalSignature>()
            .HasOne(s => s.SignedBy)
            .WithMany()
            .HasForeignKey(s => s.SignedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CompanyStampAsset>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Notification>()
            .HasOne(n => n.UatProject)
            .WithMany()
            .HasForeignKey(n => n.UatProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UatProject>().Property(p => p.Status).HasConversion<string>();
        builder.Entity<ProjectUatAttempt>().Property(a => a.OverallStatus).HasConversion<string>();
        builder.Entity<ProjectUatResult>().Property(r => r.PassStatus).HasConversion<string>();
        builder.Entity<ProjectUatResult>().Property(r => r.LastModifiedByRole).HasConversion<string>();
        builder.Entity<UatMasterItem>().Property(m => m.AnswerFormat).HasConversion<string>();
        builder.Entity<DigitalSignature>().Property(s => s.SignatoryRole).HasConversion<string>();
    }
}
