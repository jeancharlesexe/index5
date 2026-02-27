using Index5.Domain.Entities;
using Index5.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Index5.Infrastructure.Data;

public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ContaGrafica> ContasGraficas => Set<ContaGrafica>();
    public DbSet<CustodiaFilhote> CustodiasFilhote => Set<CustodiaFilhote>();
    public DbSet<CustodiaMaster> CustodiaMaster => Set<CustodiaMaster>();
    public DbSet<CestaRecomendacao> CestasRecomendacao => Set<CestaRecomendacao>();
    public DbSet<ItemCesta> ItensCesta => Set<ItemCesta>();
    public DbSet<OrdemCompra> OrdensCompra => Set<OrdemCompra>();
    public DbSet<Distribuicao> Distribuicoes => Set<Distribuicao>();

    public async Task<int> SaveChangesAsync() => await base.SaveChangesAsync();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Cpf).IsUnique();
            entity.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Cpf).HasMaxLength(11).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ValorMensal).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.ContaGrafica)
                  .WithOne(c => c.Cliente)
                  .HasForeignKey<ContaGrafica>(c => c.ClienteId);
        });

        modelBuilder.Entity<ContaGrafica>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NumeroConta).IsUnique();
            entity.Property(e => e.NumeroConta).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Tipo).HasMaxLength(10).IsRequired();
        });

        modelBuilder.Entity<CustodiaFilhote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.PrecoMedio).HasColumnType("decimal(18,4)");

            entity.HasOne(e => e.ContaGrafica)
                  .WithMany(c => c.Custodias)
                  .HasForeignKey(e => e.ContaGraficaId);
        });

        modelBuilder.Entity<CustodiaMaster>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.PrecoMedio).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Origem).HasMaxLength(200);
        });

        modelBuilder.Entity<CestaRecomendacao>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nome).HasMaxLength(100).IsRequired();

            entity.HasMany(e => e.Itens)
                  .WithOne(i => i.CestaRecomendacao)
                  .HasForeignKey(i => i.CestaRecomendacaoId);
        });

        modelBuilder.Entity<ItemCesta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Percentual).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<OrdemCompra>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();
            entity.Property(e => e.PrecoUnitario).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ValorTotal).HasColumnType("decimal(18,2)");

            entity.HasMany(e => e.Distribuicoes)
                  .WithOne(d => d.OrdemCompra)
                  .HasForeignKey(d => d.OrdemCompraId);
        });

        modelBuilder.Entity<Distribuicao>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(10).IsRequired();

            entity.HasOne(e => e.Cliente)
                  .WithMany(c => c.Distribuicoes)
                  .HasForeignKey(e => e.ClienteId);
        });
    }
}
