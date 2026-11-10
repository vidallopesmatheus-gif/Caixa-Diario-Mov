using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Data;

// Deploy de 09/11: a migration AdicionarConciliacaoSaldo subiu no código mas nunca foi aplicada
// no banco — toda listagem de contas virou 500. Agora o start aplica o que estiver pendente antes
// de aceitar requisições (e antes dos jobs em segundo plano), com log de cada uma. Se falhar, o
// processo não sobe: melhor o deploy falhar do que servir um app que quebra em toda tela.
// Desligado por padrão em Development: o .env local pode apontar pro banco de produção, e um
// "dotnet run" de uma branch qualquer não pode aplicar a migration dela lá.
public static class MigracoesNoStart
{
    public static void Aplicar(WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:AplicarMigracoesNoStart", !app.Environment.IsDevelopment())) return;

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(MigracoesNoStart));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendentes = db.Database.GetPendingMigrations().ToList();
        if (pendentes.Count == 0)
        {
            logger.LogInformation("Banco em dia: nenhuma migration pendente.");
            return;
        }

        logger.LogWarning("Aplicando {Quantidade} migration(s) pendente(s): {Migrations}", pendentes.Count, string.Join(", ", pendentes));
        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Falha ao aplicar migrations — o app não vai subir.");
            throw;
        }
        logger.LogInformation("Migrations aplicadas: {Migrations}", string.Join(", ", pendentes));
    }
}
