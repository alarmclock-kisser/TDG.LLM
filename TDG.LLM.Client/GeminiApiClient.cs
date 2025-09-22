using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TDG.LLM.Client
{
	/// <summary>
	/// Instanzbasierter Client für Googles Gemini (Generative Language API) via API-Key.
	/// Fallback-Reihenfolge API-Key:
	/// 1) explizit im Konstruktor übergeben
	/// 2) Environment Variable GEMINI_API_KEY
	/// 3) Textdatei (Standard: gemini_api_key.txt) im BaseDirectory oder angegebenem relativen Pfad
	/// </summary>
	public class GeminiApiClient
	{
		private readonly HttpClient http;
		private readonly JsonSerializerOptions jsonOpts;
		private readonly string apiKey;
		private string model; // jetzt veränderbar
		private readonly string? apiKeyFilePath;

		public string CurrentModel => model;
		public void SetModel(string? newModel)
		{
			if (!string.IsNullOrWhiteSpace(newModel)) model = newModel.Trim();
		}

		/// <summary>
		/// Erstellt einen GeminiApiClient.
		/// </summary>
		/// <param name="apiKey">Optionaler API Key. Wenn null -> Fallback Mechanismus.</param>
		/// <param name="model">Gemini Modell-ID (z.B. gemini-1.5-flash, gemini-1.5-pro)</param>
		/// <param name="apiKeyFileRelativePath">Relative oder absolute Pfadangabe zu einer Textdatei mit dem API Key (Standard: gemini_api_key.txt)</param>
		/// <param name="httpClient">Optionaler HttpClient (ansonsten eigener)</param>
		public GeminiApiClient(
			string? apiKey = null,
			string? model = "gemini-1.5-flash",
			string? apiKeyFileRelativePath = "geminiApiKey.txt",
			HttpClient? httpClient = null)
		{
			this.http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
			this.jsonOpts = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			this.apiKeyFilePath = ResolveApiKeyFilePath(apiKeyFileRelativePath);
			this.apiKey = this.NormalizeApiKey(apiKey) ?? string.Empty;
			this.model = string.IsNullOrWhiteSpace(model) ? "gemini-1.5-flash" : model.Trim();
		}

		private static string? ResolveApiKeyFilePath(string? path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return null;
			}

			if (Path.IsPathRooted(path))
			{
				return path;
			}

			var baseDir = AppContext.BaseDirectory;
			var candidates = new[]
			{
				Path.Combine(baseDir, path),
				Path.Combine(baseDir, "Secrets", path),
				Path.Combine(Directory.GetCurrentDirectory(), path)
			};
			return candidates.FirstOrDefault(File.Exists) ?? candidates.First();
		}

		private string? NormalizeApiKey(string? direct)
		{
			if (!string.IsNullOrWhiteSpace(direct))
			{
				return direct.Trim();
			}

			var env = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
			if (!string.IsNullOrWhiteSpace(env))
			{
				return env.Trim();
			}

			if (!string.IsNullOrWhiteSpace(this.apiKeyFilePath) && File.Exists(this.apiKeyFilePath))
			{
				try
				{
					var fileKey = File.ReadAllText(this.apiKeyFilePath).Trim();
					if (!string.IsNullOrWhiteSpace(fileKey))
					{
						return fileKey;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("Could not read Gemini API key file: " + ex.Message);
				}
			}
			return null;
		}

		private bool HasApiKey => !string.IsNullOrWhiteSpace(this.apiKey);

		public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
			=> this.GenerateContentAsync(new[] { prompt }, null, ct);

		public Task<string> GenerateVisionAsync(string prompt, string base64Image, string mimeType = "image/png", CancellationToken ct = default)
			=> this.GenerateContentAsync(new[] { prompt }, new[] { new InlineImage(base64Image, mimeType) }, ct);

		/// <summary>
		/// Vision/Text mit Modell-Override ohne dauerhafte Änderung.
		/// </summary>
		public Task<string> GenerateWithModelAsync(string modelOverride, IEnumerable<string>? textParts, IEnumerable<InlineImage>? images, CancellationToken ct = default)
		{
			var old = this.model;
			try
			{
				SetModel(modelOverride);
				return this.GenerateContentAsync(textParts, images, ct);
			}
			finally
			{
				this.model = old; // zurücksetzen
			}
		}

		public async Task<string> GenerateContentAsync(IEnumerable<string>? textParts, IEnumerable<InlineImage>? inlineImages, CancellationToken ct = default)
		{
			if (!this.HasApiKey)
			{
				return "Gemini API key missing (env GEMINI_API_KEY or key file).";
			}

			try
			{
				var url = $"https://generativelanguage.googleapis.com/v1beta/models/{this.model}:generateContent?key={Uri.EscapeDataString(this.apiKey)}";
				var parts = new List<object>();
				if (textParts != null)
				{
					foreach (var t in textParts.Where(s => !string.IsNullOrWhiteSpace(s)))
					{
						parts.Add(new { text = t });
					}
				}

				if (inlineImages != null)
				{
					foreach (var i in inlineImages.Where(i => !string.IsNullOrWhiteSpace(i.Base64Data)))
					{
						parts.Add(new { inline_data = new { mime_type = string.IsNullOrWhiteSpace(i.MimeType) ? "image/png" : i.MimeType, data = i.Base64Data } });
					}
				}

				if (parts.Count == 0)
				{
					return "No content (text or images).";
				}

				var requestObj = new { contents = new[] { new { parts } }, generationConfig = new { temperature = 0.2, maxOutputTokens = 1024 } };
				var json = JsonSerializer.Serialize(requestObj, this.jsonOpts);
				using var content = new StringContent(json, Encoding.UTF8, "application/json");
				using var resp = await this.http.PostAsync(url, content, ct);
				var respJson = await resp.Content.ReadAsStringAsync(ct);
				if (!resp.IsSuccessStatusCode)
				{
					return $"Gemini error {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(respJson, 600)}";
				}

				var parsed = JsonSerializer.Deserialize<GeminiResponse>(respJson, this.jsonOpts);
				if (parsed?.Candidates == null || parsed.Candidates.Count == 0)
				{
					return "(no candidates)";
				}

				var sb = new StringBuilder();
				foreach (var c in parsed.Candidates)
				{
					var partsArr = c.Content?.Parts; if (partsArr == null)
					{
						continue;
					}

					foreach (var p in partsArr)
					{
						if (!string.IsNullOrWhiteSpace(p.Text)) { if (sb.Length > 0) { sb.AppendLine(); } sb.Append(p.Text.Trim()); }
					}
				}
				return sb.Length == 0 ? "(no text response)" : sb.ToString();
			}
			catch (TaskCanceledException) { return "Request canceled (timeout or user cancellation)."; }
			catch (Exception ex) { return "Exception: " + ex.Message; }
		}

		private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "...";
		public record InlineImage(string Base64Data, string MimeType);
	}

	#region Gemini DTOs (Response)
	public class GeminiResponse { [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; } }
	public class GeminiCandidate { [JsonPropertyName("content")] public GeminiContent? Content { get; set; } [JsonPropertyName("finishReason")] public string? FinishReason { get; set; } }
	public class GeminiContent { [JsonPropertyName("parts")] public List<GeminiPart>? Parts { get; set; } }
	public class GeminiPart { [JsonPropertyName("text")] public string? Text { get; set; } [JsonPropertyName("inline_data")] public GeminiInlineData? InlineData { get; set; } }
	public class GeminiInlineData { [JsonPropertyName("mime_type")] public string? MimeType { get; set; } [JsonPropertyName("data")] public string? Data { get; set; } }
	#endregion
}