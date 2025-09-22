using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.AI;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TDG.LLM.Shared;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Google.Cloud.AIPlatform.V1;
using GPB = Google.Protobuf.WellKnownTypes;

namespace TDG.LLM.Client
{
	public class ApiClient
	{
		private readonly InternalClient internalClient;
		private readonly HttpClient httpClient;
		private readonly string baseUrl;

		public readonly PredictionServiceClient GoogleClient;

		public string BaseUrl => this.baseUrl;

		public ApiClient(HttpClient httpClient)
		{
			this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			this.baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? throw new InvalidOperationException("HttpClient.BaseAddress is not set. Configure it in DI registration.");

			this.internalClient = new InternalClient(this.baseUrl, this.httpClient);

			var credentialsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
			if (string.IsNullOrWhiteSpace(credentialsPath))
			{
				credentialsPath = "D:/tdgllm-480416fdde3f.json"; // fallback (anpassen/entfernen)
			}

			var builder = new PredictionServiceClientBuilder
			{
				Endpoint = Environment.GetEnvironmentVariable("GOOGLE_VERTEX_LOCATION_ENDPOINT") ?? "us-central1-aiplatform.googleapis.com",
				CredentialsPath = credentialsPath,
			};

			this.GoogleClient = builder.Build();
		}

		public ApiClient(string baseUrl = "https://localhost:7230") : this(new HttpClient { BaseAddress = new Uri(baseUrl) }) { }

		public async Task<IEnumerable<ImageObjInfo>> GetImageListAsync()
		{
			try { return (await this.internalClient.ListAsync()).ToList(); } catch (Exception ex) { Console.WriteLine(ex); return []; }
		}

		public async Task<ImageObjInfo> UploadImageAsync(FileParameter file)
		{
			try { return await this.internalClient.LoadAsync(file); } catch (Exception ex) { Console.WriteLine(ex); return new ImageObjInfo(); }
		}

		public async Task<ImageObjInfo> UploadImageAsync(IBrowserFile browserFile)
		{
			try
			{
				using var content = new MultipartFormDataContent();
				await using var stream = browserFile.OpenReadStream(long.MaxValue);
				var sc = new StreamContent(stream);
				sc.Headers.ContentType = new MediaTypeHeaderValue(browserFile.ContentType ?? "application/octet-stream");
				content.Add(sc, "file", browserFile.Name);
				var response = await this.httpClient.PostAsync("api/image/load", content);
				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine(await response.Content.ReadAsStringAsync());
					return new ImageObjInfo();
				}
				var json = await response.Content.ReadAsStringAsync();
				return JsonSerializer.Deserialize<ImageObjInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ImageObjInfo();
			}
			catch (Exception ex) { Console.WriteLine("Upload exception: " + ex); return new ImageObjInfo(); }
		}

		public async Task DeleteImageAsync(Guid id) { try { await this.internalClient.DeleteAsync(id); } catch (Exception ex) { Console.WriteLine(ex); } }
		public async Task ClearImagesAsync() { try { await this.internalClient.ClearAsync(); } catch (Exception ex) { Console.WriteLine(ex); } }
		public async Task<ImageData> GetBase64String(Guid id, int frameId = 0) { try { return await this.internalClient.DataAsync(id, frameId); } catch (Exception ex) { Console.WriteLine(ex); return new ImageData(); } }

		/// <summary>
		/// Multimodale Analyse via Vertex AI Prediction (Gemini). Erwartete Env Vars:
		/// GOOGLE_VERTEX_PROJECT, GOOGLE_VERTEX_LOCATION (z.B. us-central1), GOOGLE_VERTEX_MODEL (z.B. gemini-1.5-flash)
		/// GOOGLE_APPLICATION_CREDENTIALS zeigt auf Service Account JSON.
		/// </summary>
		public async Task<string> GetGoogleAiResponse(Guid id, int frameId = 0, string prompt = "What is visible in that image?")
		{
			try
			{
				var imgData = await this.GetBase64String(id, frameId);
				if (imgData == null || imgData.Id == Guid.Empty || string.IsNullOrWhiteSpace(imgData.Base64String))
				{
					return "Image data not found.";
				}

				var project = Environment.GetEnvironmentVariable("GOOGLE_VERTEX_PROJECT") ?? "tdgllm";
				var location = Environment.GetEnvironmentVariable("GOOGLE_VERTEX_LOCATION") ?? "us-central1";
				var model = Environment.GetEnvironmentVariable("GOOGLE_VERTEX_MODEL") ?? "gemini-1.5-flash";
				var endpointName = $"projects/{project}/locations/{location}/publishers/google/models/{model}";

				string imageMime = string.IsNullOrWhiteSpace(imgData.Format) ? "image/png" : $"image/{imgData.Format}";

				// Build instance Value manually
				var promptPart = new GPB.Value
				{
					StructValue = new GPB.Struct { Fields = { ["text"] = GPB.Value.ForString(prompt) } }
				};
				var imagePart = new GPB.Value
				{
					StructValue = new GPB.Struct
					{
						Fields =
						{
							["inline_data"] = new GPB.Value
							{
								StructValue = new GPB.Struct
								{
									Fields =
									{
										["mime_type"] = GPB.Value.ForString(imageMime),
										["data"] = GPB.Value.ForString(imgData.Base64String)
									}
								}
							}
						}
					}
				};

				var partsArray = new GPB.Value { ListValue = new GPB.ListValue { Values = { promptPart, imagePart } } };
				var contentStruct = new GPB.Struct { Fields = { ["parts"] = partsArray } };
				var instance = new GPB.Value { StructValue = new GPB.Struct { Fields = { ["content"] = new GPB.Value { StructValue = contentStruct } } } };

				var predictRequest = new PredictRequest { Endpoint = endpointName };
				predictRequest.Instances.Add(instance);

				// Parameters
				var parameters = new GPB.Value
				{
					StructValue = new GPB.Struct
					{
						Fields =
						{
							["temperature"] = GPB.Value.ForNumber(0.2),
							["max_output_tokens"] = GPB.Value.ForNumber(512)
						}
					}
				};
				predictRequest.Parameters = parameters;

				var predictResponse = await this.GoogleClient.PredictAsync(predictRequest);
				if (predictResponse == null || predictResponse.Predictions.Count == 0)
				{
					return "No prediction returned.";
				}

				var sb = new StringBuilder();
				foreach (var prediction in predictResponse.Predictions)
				{
					try
					{
						using var doc = JsonDocument.Parse(prediction.ToString());
						if (doc.RootElement.TryGetProperty("candidates", out var candidates))
						{
							foreach (var c in candidates.EnumerateArray())
							{
								if (c.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
								{
									foreach (var p in parts.EnumerateArray())
									{
										if (p.TryGetProperty("text", out var textElem))
										{
											var t = textElem.GetString();
											if (!string.IsNullOrWhiteSpace(t))
											{
												if (sb.Length > 0)
												{
													sb.Append('\n');
												}

												sb.Append(t);
											}
										}
									}
								}
							}
						}
					}
					catch (Exception pe) { Console.WriteLine("Prediction parse error: " + pe.Message); }
				}

				return sb.Length == 0 ? "(no text)" : sb.ToString();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				return "Error getting AI response: " + Environment.NewLine + ex.Message + Environment.NewLine + ex.InnerException?.Message;
			}
		}
	}
}
