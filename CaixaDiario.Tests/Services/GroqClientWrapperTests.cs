using System.Net;
using System.Text;
using CaixaDiario.API.DTOs.Chat;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class GroqClientWrapperTests
{
    private static HttpClient BuildClient(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler(status, body);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task EnviarMensagemAsync_RespostaValida_RetornaConteudoDoAssistente()
    {
        var groqResponse = """
            {
                "choices": [
                    {
                        "message": {
                            "role": "assistant",
                            "content": "Para registrar uma entrada, acesse a tela Caixa."
                        }
                    }
                ]
            }
            """;
        var sut = new GroqClientWrapper(
            BuildClient(HttpStatusCode.OK, groqResponse),
            "test-key", "llama-3.3-70b-versatile", 1024);

        var result = await sut.EnviarMensagemAsync("system prompt", [], "como usar o app?");

        Assert.Equal("Para registrar uma entrada, acesse a tela Caixa.", result);
    }

    [Fact]
    public async Task EnviarMensagemAsync_ErroHTTP_LancaHttpRequestException()
    {
        var sut = new GroqClientWrapper(
            BuildClient(HttpStatusCode.Unauthorized, """{"error":{"message":"invalid api key"}}"""),
            "bad-key", "llama-3.3-70b-versatile", 1024);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.EnviarMensagemAsync("system", [], "mensagem"));
    }

    [Fact]
    public async Task EnviarMensagemAsync_ComHistorico_EnviaSystemPrimeiroDepoisHistoricoDepoisUser()
    {
        string? capturedBody = null;
        var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""",
            body => capturedBody = body);
        var sut = new GroqClientWrapper(new HttpClient(handler), "key", "model", 1024);

        var historico = new List<ChatMessageDto>
        {
            new() { Role = "user", Content = "primeira pergunta" },
            new() { Role = "assistant", Content = "primeira resposta" }
        };

        await sut.EnviarMensagemAsync("meu system prompt", historico, "nova pergunta");

        Assert.NotNull(capturedBody);
        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody);
        var msgs = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(4, msgs.Count);
        Assert.Equal("system", msgs[0].GetProperty("role").GetString());
        Assert.Equal("user",   msgs[1].GetProperty("role").GetString());
        Assert.Equal("assistant", msgs[2].GetProperty("role").GetString());
        Assert.Equal("user",   msgs[3].GetProperty("role").GetString());
        Assert.Equal("meu system prompt", msgs[0].GetProperty("content").GetString());
        Assert.Equal("nova pergunta", msgs[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task EnviarMensagemAsync_EnviaModelEMaxTokensCorretos()
    {
        string? capturedBody = null;
        var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"ok"}}]}""",
            body => capturedBody = body);
        var sut = new GroqClientWrapper(new HttpClient(handler), "key", "llama-3.3-70b-versatile", 512);

        await sut.EnviarMensagemAsync("system", [], "msg");

        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody!);
        Assert.Equal("llama-3.3-70b-versatile", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal(512, doc.RootElement.GetProperty("max_tokens").GetInt32());
    }
}

internal class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

internal class CapturingHandler(HttpStatusCode status, string body, Action<string> capture) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var requestBody = await request.Content!.ReadAsStringAsync(ct);
        capture(requestBody);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
