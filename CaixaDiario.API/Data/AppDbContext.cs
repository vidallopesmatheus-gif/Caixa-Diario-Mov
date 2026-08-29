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
    public DbSet<ContaBancaria> ContasBancarias { get; set; }
    public DbSet<TransacaoImportada> TransacoesImportadas { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Transferencia> Transferencias { get; set; }

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
                    v => JsonSerializer.Deserialize<List<ItemFinanceiroSaida>>(v, _jsonOptions) ?? new());
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

            entity.Property(e => e.ContaBancariaId).HasColumnName("conta_bancaria_id");

            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.Registros)
                .HasForeignKey(e => e.ClienteId);

            entity.HasOne(e => e.ContaBancaria)
                .WithMany(c => c.Registros)
                .HasForeignKey(e => e.ContaBancariaId)
                .IsRequired(false);

            entity.HasIndex(e => new { e.ClienteId, e.Data });
            entity.HasIndex(e => new { e.ContaBancariaId, e.Data }).IsUnique()
                .HasFilter("conta_bancaria_id IS NOT NULL AND excluido = FALSE");
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
            entity.Property(e => e.ContaInvestimentoId).HasColumnName("conta_investimento_id");
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
            entity.Property(e => e.DataAlvo).HasColumnName("data_alvo");

            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.MetasAnuais)
                .HasForeignKey(e => e.ClienteId);

            entity.HasOne(e => e.ContaInvestimento)
                .WithMany()
                .HasForeignKey(e => e.ContaInvestimentoId)
                .IsRequired(false);

            // Só o modo "simples" (Meta de Faturamento Mensal) é 1-por-ano-civil; o modo "metodo"
            // (objetivos) pode ter N metas simultâneas pro mesmo cliente, independente do ano.
            entity.HasIndex(e => new { e.ClienteId, e.Ano })
                .IsUnique()
                .HasFilter("\"ModoMeta\" = 'simples'");
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
            entity.Property(e => e.Periodicidade).HasColumnName("periodicidade").IsRequired().HasDefaultValue("Mensal");
            entity.Property(e => e.QuantidadeParcelas).HasColumnName("quantidade_parcelas");
            entity.Property(e => e.Ativo).HasColumnName("ativo").HasDefaultValue(true);
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");
            entity.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.ContasRecorrentes)
                .HasForeignKey(e => e.ClienteId);
            entity.HasIndex(e => new { e.ClienteId, e.Ativo });
        });

        modelBuilder.Entity<ContaBancaria>(entity =>
        {
            entity.ToTable("contas_bancarias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.Nome).HasColumnName("nome").IsRequired();
            entity.Property(e => e.Tipo).HasColumnName("tipo").IsRequired().HasDefaultValue("Caixa");
            entity.Property(e => e.SaldoInicial).HasColumnName("saldo_inicial").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.Ativa).HasColumnName("ativa").HasDefaultValue(true);
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao").HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.ContasBancarias)
                .HasForeignKey(e => e.ClienteId);

            entity.HasIndex(e => new { e.ClienteId, e.Ativa });
        });

        modelBuilder.Entity<TransacaoImportada>(entity =>
        {
            entity.ToTable("transacoes_importadas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ContaBancariaId).HasColumnName("conta_bancaria_id");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.Data).HasColumnName("data");
            entity.Property(e => e.Valor).HasColumnName("valor").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Descricao).HasColumnName("descricao").IsRequired();
            entity.Property(e => e.FitId).HasColumnName("fit_id");
            entity.Property(e => e.Tipo).HasColumnName("tipo").IsRequired().HasDefaultValue("Entrada");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired().HasDefaultValue("Pendente");
            entity.Property(e => e.Categoria).HasColumnName("categoria");
            entity.Property(e => e.ImportadoEm).HasColumnName("importado_em").HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.ContaBancaria)
                .WithMany()
                .HasForeignKey(e => e.ContaBancariaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ContaBancariaId, e.Status });
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

        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("categorias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nome).HasColumnName("nome").IsRequired();
            entity.Property(e => e.Tipo).HasColumnName("tipo").IsRequired();
            entity.Property(e => e.Grupo).HasColumnName("grupo");
            entity.Property(e => e.Ordem).HasColumnName("ordem").HasDefaultValue(0);
            entity.Property(e => e.Ativa).HasColumnName("ativa").HasDefaultValue(true);
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.Nome).IsUnique();
            entity.HasIndex(e => new { e.Ativa, e.Ordem });
        });

        modelBuilder.Entity<Transferencia>(entity =>
        {
            entity.ToTable("transferencias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.ContaOrigemId).HasColumnName("conta_origem_id");
            entity.Property(e => e.ContaDestinoId).HasColumnName("conta_destino_id");
            entity.Property(e => e.Data).HasColumnName("data");
            entity.Property(e => e.Valor).HasColumnName("valor").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em").HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Cliente)
                .WithMany()
                .HasForeignKey(e => e.ClienteId);

            entity.HasOne(e => e.ContaOrigem)
                .WithMany()
                .HasForeignKey(e => e.ContaOrigemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ContaDestino)
                .WithMany()
                .HasForeignKey(e => e.ContaDestinoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ClienteId, e.Data });
        });
    }
}
