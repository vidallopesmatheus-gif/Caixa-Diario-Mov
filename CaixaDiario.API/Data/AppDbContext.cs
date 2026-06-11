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
    public DbSet<MetaAnual> MetasAnuais { get; set; }
    public DbSet<ContaRecorrente> ContasRecorrentes { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

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
            entity.Property(e => e.Entradas).HasColumnName("entradas").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemFinanceiro>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.Saidas).HasColumnName("saidas").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ItemFinanceiro>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.ContasReceber).HasColumnName("contas_receber").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ContaProvisionada>>(v, _jsonOptions) ?? new());
            entity.Property(e => e.ContasPagar).HasColumnName("contas_pagar").HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, _jsonOptions),
                    v => JsonSerializer.Deserialize<List<ContaProvisionada>>(v, _jsonOptions) ?? new());
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

        modelBuilder.Entity<MetaAnual>(entity =>
        {
            entity.ToTable("metas_anuais");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.Ano).HasColumnName("ano");
            entity.Property(e => e.MetaReceita).HasColumnName("meta_receita").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MetaLucro).HasColumnName("meta_lucro").HasColumnType("decimal(18,2)");
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");

            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.MetasAnuais)
                .HasForeignKey(e => e.ClienteId);

            entity.HasIndex(e => new { e.ClienteId, e.Ano }).IsUnique();
        });

        modelBuilder.Entity<ContaRecorrente>(entity =>
        {
            entity.ToTable("contas_recorrentes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(e => e.Valor).HasColumnName("valor").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Categoria).HasColumnName("categoria");
            entity.Property(e => e.Tipo).HasColumnName("tipo").IsRequired();
            entity.Property(e => e.DataInicio).HasColumnName("data_inicio");
            entity.Property(e => e.DataFim).HasColumnName("data_fim");
            entity.Property(e => e.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.ContasRecorrentes)
                .HasForeignKey(e => e.ClienteId);
            entity.HasIndex(e => new { e.ClienteId, e.Ativo });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
            entity.Property(e => e.Entidade).HasColumnName("entidade").IsRequired();
            entity.Property(e => e.AcaoTipo).HasColumnName("acao_tipo").IsRequired();
            entity.Property(e => e.EntidadeId).HasColumnName("entidade_id").IsRequired();
            entity.Property(e => e.DadosAntes).HasColumnName("dados_antes");
            entity.Property(e => e.DadosDepois).HasColumnName("dados_depois");
            entity.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em").HasDefaultValueSql("NOW()");
            entity.HasIndex(e => new { e.ClienteId, e.OcorridoEm });
            entity.HasIndex(e => new { e.Entidade, e.AcaoTipo });
        });
    }
}
