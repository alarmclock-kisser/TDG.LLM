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

namespace TDG.LLM.Client
{
	public class ApiClient
	{
		private readonly InternalClient internalClient;
		private readonly OllamaApiClient ollamaClient;
		private readonly HttpClient httpClient; // store http client for manual multipart calls

		public ApiClient(string baseUrl = "https://localhost:7230")
		{
			this.httpClient = new HttpClient
			{
				BaseAddress = new Uri(baseUrl)
			};

			this.internalClient = new InternalClient(baseUrl, this.httpClient);

			this.ollamaClient = new OllamaApiClient("http://localhost:11434", "llava:7b");
		}

		public async Task<IEnumerable<ImageObjInfo>> GetImageListAsync()
		{
			var response = new List<ImageObjInfo>();

			try
			{
				response = (await this.internalClient.ListAsync()).ToList();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			return response;
		}

		public async Task<ImageObjInfo> UploadImageAsync(FileParameter file)
		{
			var task = this.internalClient.LoadAsync(file);

			try
			{
				return await task;
			}
			catch (Exception exception)
			{
				Console.WriteLine(exception);
				return new ImageObjInfo();
			}
		}

		// Convenience overload for Blazor IBrowserFile
		public async Task<ImageObjInfo> UploadImageAsync(IBrowserFile browserFile)
		{
			try
			{
				using var content = new MultipartFormDataContent();
				await using var stream = browserFile.OpenReadStream(long.MaxValue);
				var sc = new StreamContent(stream);
				sc.Headers.ContentType = new MediaTypeHeaderValue(browserFile.ContentType);
				content.Add(sc, "file", browserFile.Name);

				var response = await this.httpClient.PostAsync("api/image/load", content);
				if (!response.IsSuccessStatusCode)
				{
					return new ImageObjInfo();
				}

				var json = await response.Content.ReadAsStringAsync();
				var info = JsonSerializer.Deserialize<ImageObjInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ImageObjInfo();
				return info;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				return new ImageObjInfo();
			}
		}

		public async Task DeleteImageAsync(Guid id)
		{
			try
			{
				await this.internalClient.DeleteAsync(id);
			}
			catch (Exception exception)
			{
				Console.WriteLine(exception);
			}
		}

		public async Task ClearImagesAsync()
		{
			try
			{
				await this.internalClient.ClearAsync();
			}
			catch (Exception exception)
			{
				Console.WriteLine(exception);
			}
		}

		public async Task<ImageData> GetBase64String(Guid id, int frameId = 0)
		{
			try
			{
				return await this.internalClient.DataAsync(id, frameId);
			}
			catch (Exception exception)
			{
				Console.WriteLine(exception);
				return new ImageData();
			}
		}

		public async Task<string> GetOllamaResponseAsync(Guid id, int frameId = 0, string prompt = "What can you see in the given picture?")
		{
			try
			{
				var imgData = await this.internalClient.DataAsync(id, frameId);
				if (imgData == null)
				{
					return "Image data not found.";
				}

				this.ollamaClient.SelectedModel = "gemma3:12b";
				var fullPrompt = $"{prompt}\n![image](data:image/{imgData.Format};base64,{imgData.Base64String})";
				var response = await this.ollamaClient.GetResponseAsync(fullPrompt);

				return response.Text;
			}
			catch (Exception exception)
			{
				Console.WriteLine(exception);
				return "Error getting response from Ollama.";
			}
		}
	}
}
