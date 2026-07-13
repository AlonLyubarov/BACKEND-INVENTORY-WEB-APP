using AlonProject.Domain.Entities;
using AlonProject.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AlonProject.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProductCatalog> ProductCatalogs { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Shift> Shifts { get; set; }
    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<PersonalTask> PersonalTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ProductCatalog configuration
        modelBuilder.Entity<ProductCatalog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sku).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Barcode).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasMany(e => e.Items).WithOne(i => i.ProductCatalog).HasForeignKey(i => i.ProductCatalogId).OnDelete(DeleteBehavior.Cascade);

            // Tenant scoping: each product belongs to one owner's catalog
            entity.HasIndex(e => e.OwnerId);
            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Item configuration
        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MinimumStockLevel).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // SECURITY: Item must belong to a warehouse for data isolation
            entity.HasOne(e => e.Warehouse)
                .WithMany(w => w.Items)
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(true);

            entity.HasOne(e => e.ProductCatalog)
                .WithMany(p => p.Items)
                .HasForeignKey(e => e.ProductCatalogId)
                .OnDelete(DeleteBehavior.Cascade);

            // SECURITY: Prevent cascade delete of transactions (audit trail integrity)
            // If an item exists, we keep its transaction history.
            // To delete an item, admins must use soft-delete or explicitly archive transactions.
            entity.HasMany(e => e.Transactions)
                .WithOne(t => t.Item)
                .HasForeignKey(t => t.ItemId)
                .OnDelete(DeleteBehavior.Restrict);  // Changed from Cascade to Restrict
        });

        // Transaction configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(e => e.Item).WithMany(i => i.Transactions).HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Cascade);
        });

        // Warehouse configuration
        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // OwnerId FK: Admin user who owns this warehouse (null for sub-warehouses)
            entity.HasOne(e => e.Owner)
                .WithMany(u => u.OwnedWarehouses)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ParentWarehouseId self-FK: hierarchical warehouse structure
            // Null for main warehouses, references parent for sub-warehouses
            entity.HasOne(e => e.Parent)
                .WithMany(w => w.SubWarehouses)
                .HasForeignKey(e => e.ParentWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Users assigned to this warehouse
            entity.HasMany(e => e.Users).WithOne(u => u.Warehouse).HasForeignKey(u => u.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        // Shift (work schedule) configuration
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(300);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            // Schedule lookups are per user or per warehouse, by date
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.HasIndex(e => new { e.WarehouseId, e.Date });
            // Removing a user removes their shifts
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // Restrict here is safe: a warehouse can only be deleted once it has
            // no users, and by then all its shifts were cascaded away with them
            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Reminder configuration
        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            // Calendar lookups are always per user, usually per date range
            entity.HasIndex(e => new { e.UserId, e.Date });
            // Deleting a user removes their personal reminders with them
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PersonalTask configuration
        modelBuilder.Entity<PersonalTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            // The task list is always fetched per user
            entity.HasIndex(e => new { e.UserId, e.IsCompleted });
            // Deleting a user removes their personal tasks with them
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Email).IsUnique();  // SECURITY: Enforce unique emails to prevent duplicate registrations
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.EmailVerified).HasDefaultValue(false);
            entity.Property(e => e.EmailVerificationToken).HasMaxLength(100);
            entity.HasIndex(e => e.EmailVerificationToken);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            // WarehouseId is nullable: user starts with null, assigned later via invitation
            entity.HasOne(e => e.Warehouse).WithMany(w => w.Users).HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });
    }
}
