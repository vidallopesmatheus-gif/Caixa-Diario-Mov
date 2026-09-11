using System.Text;
using CaixaDiario.API.Data;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Middleware;
using CaixaDiario.API.Repositories;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Erro de model binding/DataAnnotations (ex.: [Required] faltando) normalmente vira um 400 do
// próprio framework, no formato ValidationProblemDetails — sem "mensagem", o formato que o
// frontend (apiFetch) sabe ler. Redireciona esse 400 pro mesmo formato de ErroResponse que
// ErrorHandlingMiddleware usa pra ApiException, pra nunca virar um "Erro 400" sem contexto.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var primeiroErro = context.ModelState
            .FirstOrDefault(kvp => kvp.Value?.Errors.Count > 0);
        var mensagem = primeiroErro.Value?.Errors.FirstOrDefault()?.ErrorMessage ?? "Dados inválidos.";
        var campo = string.IsNullOrEmpty(primeiroErro.Key) ? null : primeiroErro.Key;

        return new BadRequestObjectResult(new ErroResponse
        {
            Status = 400,
            Codigo = CodigoRetorno.DADOS_INVALIDOS.ToString(),
            Mensagem = mensagem,
            Campo = campo,
        });
    };
});
// Banco de dados
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Autenticação JWT
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

builder.Services.AddAuthorization();

// CORS
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Injeção de dependência
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IRegistroRepository, RegistroRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IRegistroService, RegistroService>();
builder.Services.AddScoped<IMetaRepository, MetaRepository>();
builder.Services.AddScoped<IMetaService, MetaService>();
builder.Services.AddScoped<IContaBancariaRepository, ContaBancariaRepository>();
builder.Services.AddScoped<IContaBancariaService, ContaBancariaService>();
builder.Services.AddScoped<ITransacaoImportadaRepository, TransacaoImportadaRepository>();
builder.Services.AddScoped<IImportacaoService, ImportacaoService>();
builder.Services.AddScoped<IContaRecorrenteRepository, ContaRecorrenteRepository>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IRecorrenciaService, RecorrenciaService>();
builder.Services.AddScoped<IContaRecorrenteService, ContaRecorrenteService>();
builder.Services.AddScoped<IMetricasService, MetricasService>();
builder.Services.AddScoped<IProjecaoService, ProjecaoService>();
builder.Services.AddScoped<IInsightService, InsightService>();
builder.Services.AddScoped<IOrcamentoDinamicoService, OrcamentoDinamicoService>();
builder.Services.AddScoped<ISaudeFinanceiraService, SaudeFinanceiraService>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IGrupoRepository, GrupoRepository>();
builder.Services.AddScoped<IGrupoService, GrupoService>();
builder.Services.AddScoped<ITransferenciaRepository, TransferenciaRepository>();
builder.Services.AddScoped<ITransferenciaService, TransferenciaService>();
builder.Services.AddScoped<IRegraCategorizacaoRepository, RegraCategorizacaoRepository>();
builder.Services.AddScoped<IRegraCategorizacaoService, RegraCategorizacaoService>();
builder.Services.AddScoped<IMetaProgressoService, MetaProgressoService>();
builder.Services.AddScoped<ILinkConciliacaoRepository, LinkConciliacaoRepository>();
builder.Services.AddScoped<ILinkConciliacaoService, LinkConciliacaoService>();
builder.Services.AddScoped<IConciliacaoPublicaService, ConciliacaoPublicaService>();
builder.Services.AddScoped<IPagamentoFaturaRepository, PagamentoFaturaRepository>();
builder.Services.AddScoped<IFaturaCartaoService, FaturaCartaoService>();

// Chat IA
var groqApiKey = builder.Configuration["Groq:ApiKey"]
    ?? throw new InvalidOperationException("Groq:ApiKey não configurada.");
var groqModel = builder.Configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
var groqMaxTokens = int.TryParse(builder.Configuration["Groq:MaxTokens"], out var mt) ? mt : 1024;
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAnthropicClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    return new GroqClientWrapper(http, groqApiKey, groqModel, groqMaxTokens);
});
builder.Services.AddScoped<IChatService, ChatService>();


var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name == "index.html")
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    }
});

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate"
});

app.Run();

