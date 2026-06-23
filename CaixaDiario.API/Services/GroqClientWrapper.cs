using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CaixaDiario.API.DTOs.Chat;

namespace CaixaDiario.API.Services;

public class GroqClientWrapper : IAnthropicClient
{
    private const string ApiUrl = "https://api.groq.com/openai/v1/chat/completions";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;

    public GroqClientWrapper(HttpClient http, string apiKey, string model, int maxTokens)
    {
        _http = http;
        _apiKey = apiKey;
        _model = model;
        _maxTokens = maxTokens;
    }

    public async Task<string> EnviarMensagemAsync(
        string systemPrompt, List<ChatMessageDto> historico, string mensagem)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var h in historico)
            messages.Add(new { role = h.Role, content = h.Content });

        messages.Add(new { role = "user", content = mensagem });

        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            max_tokens = _maxTokens,
            messages
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(error);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
