using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClawdPet
{
    public class AiHttpError : Exception
    {
        public int Code;
        public string Body;
        public AiHttpError(int code, string body) : base($"HTTP {code}") { Code = code; Body = body ?? ""; }
    }

    public class AiProviderCfg
    {
        public string Key, Url, Model, Persona;
        public int Provider;
    }

    public record AiResult(string Say, string Mood, string Action, string Remember);

    public static class Ai
    {
        static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(C.AiTimeoutLocal + 5) };

        public static string Prompt(string category, Dictionary<string, string> ctx)
        {
            string situation = C.Situations.TryGetValue(category, out var s) ? s : category;
            var lines = new List<string> { "Ситуация: " + situation, $"Сейчас {DateTime.Now:HH:mm}." };
            foreach (var (key, label) in C.CtxLabels)
            {
                if (!ctx.TryGetValue(key, out var value) || string.IsNullOrEmpty(value)) continue;
                value = string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
                if (value.Length > C.AiTextMax) value = value[..C.AiTextMax];
                lines.Add($"{label}: {value}");
            }
            return string.Join("\n", lines);
        }

        public static async Task<AiResult> Call(AiProviderCfg cfg, string category, Dictionary<string, string> ctx,
            List<string> history, string memory)
        {
            var prompt = Prompt(category, ctx);
            if (!string.IsNullOrEmpty(memory)) prompt = memory + "\n\n" + prompt;
            if (history.Count > 0) prompt += "\nТвои последние реплики (не повторяйся): " + string.Join(" | ", history);
            var system = C.AiSystem;
            if (!string.IsNullOrWhiteSpace(cfg.Persona)) system += " Дополнительно к характеру: " + cfg.Persona;
            string raw = cfg.Provider == 1 ? await CallOpenAi(cfg, system, prompt) : await CallGemini(cfg, system, prompt);
            return Parse(raw);
        }

        static async Task<string> CallGemini(AiProviderCfg cfg, string system, string prompt)
        {
            var model = string.IsNullOrWhiteSpace(cfg.Model) ? C.AiGeminiModel : cfg.Model.Trim();
            if (model.StartsWith("models/")) model = model["models/".Length..];
            var url = $"{C.AiGeminiUrl}/models/{model}:generateContent";
            var headers = new Dictionary<string, string> { ["x-goog-api-key"] = cfg.Key };
            JsonObject Body(bool thinking)
            {
                var gen = new JsonObject
                {
                    ["maxOutputTokens"] = 300,
                    ["temperature"] = 1.0,
                    ["responseMimeType"] = "application/json",
                };
                if (thinking) gen["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = "minimal" };
                return new JsonObject
                {
                    ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = system }) },
                    ["contents"] = new JsonArray(new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt }),
                    }),
                    ["generationConfig"] = gen,
                };
            }
            JsonNode data;
            try { data = await PostJson(url, headers, Body(true), C.AiTimeout); }
            catch (AiHttpError e) when (e.Code == 400 && e.Body.ToLowerInvariant().Contains("thinking"))
            {
                data = await PostJson(url, headers, Body(false), C.AiTimeout);
            }
            var parts = data["candidates"]![0]!["content"]!["parts"]!.AsArray();
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part?["thought"]?.GetValue<bool>() == true) continue;
                sb.Append(part?["text"]?.GetValue<string>() ?? "");
            }
            return sb.ToString();
        }

        static async Task<string> CallOpenAi(AiProviderCfg cfg, string system, string prompt)
        {
            var baseUrl = (string.IsNullOrWhiteSpace(cfg.Url) ? C.AiOpenAiUrl : cfg.Url).Trim().TrimEnd('/');
            if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "https://" + baseUrl;
            var url = baseUrl.EndsWith("/chat/completions") ? baseUrl : baseUrl + "/chat/completions";
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(cfg.Key)) headers["Authorization"] = "Bearer " + cfg.Key.Trim();
            var body = new JsonObject
            {
                ["model"] = string.IsNullOrWhiteSpace(cfg.Model) ? C.AiOpenAiModel : cfg.Model.Trim(),
                ["messages"] = new JsonArray(
                    new JsonObject { ["role"] = "system", ["content"] = system },
                    new JsonObject { ["role"] = "user", ["content"] = prompt }),
                ["max_tokens"] = 170,
                ["temperature"] = 0.95,
            };
            int timeout = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? C.AiTimeoutLocal : C.AiTimeout;
            var data = await PostJson(url, headers, body, timeout);
            return data["choices"]![0]!["message"]!["content"]!.GetValue<string>();
        }

        static async Task<JsonNode> PostJson(string url, Dictionary<string, string> headers, JsonObject body, int timeout)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (ClawdPet for Windows)");
            foreach (var kv in headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            using var resp = await Http.SendAsync(req, cts.Token);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new AiHttpError((int)resp.StatusCode, text);
            return JsonNode.Parse(text);
        }

        static string Grab(string raw, string key)
        {
            var m = Regex.Match(raw, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        public static AiResult Parse(string raw)
        {
            raw = (raw ?? "").Trim();
            string say = null, mood = null, action = null, remember = null;
            var match = Regex.Match(raw, @"\{.*\}", RegexOptions.Singleline);
            if (match.Success)
            {
                try
                {
                    var obj = JsonNode.Parse(match.Value)!.AsObject();
                    string Str(string k) => obj[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
                    say = Str("say");
                    mood = Str("mood").Trim().ToLowerInvariant();
                    action = Str("action").Trim().ToLowerInvariant();
                    remember = Str("remember").Trim();
                }
                catch { say = null; }
            }
            if (say == null && raw.Contains("\"say\""))
            {
                say = Grab(raw, "say") ?? "";
                mood = Grab(raw, "mood")?.ToLowerInvariant();
                action = Grab(raw, "action")?.ToLowerInvariant();
                remember = Grab(raw, "remember");
            }
            if (say == null) say = raw.TrimStart().StartsWith("{") || raw.TrimStart().StartsWith("`") ? "" : raw;
            if (mood == null || !C.Moods.Contains(mood)) mood = null;
            if (action == null || !C.Actions.Contains(action)) action = null;
            return new AiResult(CleanText(say), mood, action, string.IsNullOrWhiteSpace(remember) ? null : remember);
        }

        public static string CleanText(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return null;
            text = text.Split('\n')[0].Trim();
            text = text.Trim('"', '\'', '«', '»', '“', '”', '„', ' ');
            if (text.Length > C.AiMaxChars) text = text[..(C.AiMaxChars - 1)].TrimEnd() + "…";
            return text.Length == 0 ? null : text;
        }
    }
}
