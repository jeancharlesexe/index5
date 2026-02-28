using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Index5.Infrastructure.Data;

public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<GraphicAccount> GraphicAccounts => Set<GraphicAccount>();
    public DbSet<ChildCustody> ChildCustodies => Set<ChildCustody>();
    public DbSet<MasterCustody> MasterCustodies => Set<MasterCustody>();
    public DbSet<RecommendationBasket> RecommendationBaskets => Set<RecommendationBasket>();
    public DbSet<BasketItem> BasketItems => Set<BasketItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<Distribution> Distributions => Set<Distribution>();
    public DbSet<OperationHistory> OperationHistory => Set<OperationHistory>();
    public DbSet<User> Users => Set<User>();

    public async Task<int> SaveChangesAsync() => await base.SaveChangesAsync();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Cpf).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Cpf).HasMaxLength(11).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.MonthlyValue).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.GraphicAccount)
                  .WithOne(c => c.Client)
                  .HasForeignKey<GraphicAccount>(c => c.ClientId);
        });

        modelBuilder.Entity<GraphicAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountNumber).IsUnique();
            entity.Property(e => e.AccountNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(10).IsRequired();
        });

        modelBuilder.Entity<ChildCustody>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.AveragePrice).HasColumnType("decimal(18,4)");

            entity.HasOne(e => e.GraphicAccount)
                  .WithMany(c => c.Custodies)
                  .HasForeignKey(e => e.GraphicAccountId);
        });

        modelBuilder.Entity<MasterCustody>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.AveragePrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Origin).HasMaxLength(200);
        });

        modelBuilder.Entity<RecommendationBasket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();

            entity.HasMany(e => e.Items)
                  .WithOne(i => i.RecommendationBasket)
                  .HasForeignKey(i => i.RecommendationBasketId);
        });

        modelBuilder.Entity<BasketItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Percentage).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TotalValue).HasColumnType("decimal(18,2)");

            entity.HasMany(e => e.Distributions)
                  .WithOne(d => d.PurchaseOrder)
                  .HasForeignKey(d => d.PurchaseOrderId);
        });

        modelBuilder.Entity<Distribution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();

            entity.HasOne(e => e.Client)
                  .WithMany(c => c.Distributions)
                  .HasForeignKey(e => e.ClientId);
        });

        modelBuilder.Entity<OperationHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.OperationType).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TotalValue).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Client)
                  .WithMany()
                  .HasForeignKey(e => e.ClientId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Cpf).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Cpf).HasMaxLength(11).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(10).IsRequired();
        });
    }
}
