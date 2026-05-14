using System.Text.Json;
using CaixaDiario.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Data;

public class AppDbContext : DbContext
{
    private static readonly JsonSerializerOptions _jsonOptions = new();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<RegistroDiario> RegistrosDiarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.NomeUsuario).HasColumnName("nome_usuario").IsRequired();
            entity.HasIndex(e => e.NomeUsuario).IsUnique();
            entity.Property(e => e.SenhaHash).HasColumnName("senha_hash").IsRequired();
            entity.Property(e => e.Nome).HasColumnName("nome").IsRequired();
            entity.Property(e => e.Loja).HasColumnName("loja");
            entity.Property(e => e.Perfil).HasColumnName("perfil").IsRequired();
            entity.Property(e => e.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
            entity.Property(e => e.UsuarioAtualizacao).HasColumnName("usuario_atualizacao");
        });

        modelBuilder.Entity<RegistroDiario>(entity =>
        {
            entity.ToTable("registros_diarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.Data).HasColumnName("data");
            entity.Property(e => e.Inicio).HasColumnName("inicio").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Entrada).HasColumnName("entrada").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Saidas).HasColumnName("saidas").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemFinanceiro>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.ContasReceber).HasColumnName("contas_receber").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemFinanceiro>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.ContasPagar).HasColumnName("contas_pagar").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemFinanceiro>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.SaldoFinal).HasColumnName("saldo_final").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Excluido).HasColumnName("excluido").HasDefaultValue(false);
            entity.Property(e => e.MotivoExclusao).HasColumnName("motivo_exclusao");
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
            entity.Property(e => e.SalvoEm).HasColumnName("salvo_em");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
            entity.Property(e => e.UsuarioAtualizacao).HasColumnName("usuario_atualizacao");

            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.Registros)
                .HasForeignKey(e => e.ClienteId);

            entity.HasIndex(e => new { e.ClienteId, e.Data }).IsUnique();
        });
    }
}
