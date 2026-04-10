using Microsoft.EntityFrameworkCore;
using PlechPomoshchi.Models;

namespace PlechPomoshchi.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserFavoriteOrganization> FavoriteOrganizations => Set<UserFavoriteOrganization>();
    public DbSet<VolunteerOrgApplication> VolunteerOrgApplications => Set<VolunteerOrgApplication>();

    public DbSet<HelpRequest> Requests => Set<HelpRequest>();
    public DbSet<RequestComment> Comments => Set<RequestComment>();

    public DbSet<ParserState> ParserStates => Set<ParserState>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.FullName).IsRequired();
            e.Property(x => x.Role).IsRequired();
            e.Property(x => x.ResetToken).HasMaxLength(200);
        });

        b.Entity<Organization>(e =>
        {
            e.HasIndex(x => x.Name);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Category).IsRequired();
        });

        b.Entity<UserFavoriteOrganization>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.OrganizationId }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<HelpRequest>(e =>
        {
            e.Property(x => x.Category).IsRequired();
            e.Property(x => x.Description).IsRequired();
            e.Property(x => x.Status).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RequestComment>(e =>
        {
            e.Property(x => x.Text).IsRequired();
            e.HasOne(x => x.Request).WithMany(x => x.Comments).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<VolunteerOrgApplication>(e =>
        {
            e.Property(x => x.OrgName).IsRequired();
            e.Property(x => x.Category).IsRequired();
            e.Property(x => x.Address).IsRequired();
            e.Property(x => x.Contact).IsRequired();
            e.Property(x => x.Message).IsRequired();
        });

        b.Entity<ParserState>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).IsRequired();
        });
    }
}
