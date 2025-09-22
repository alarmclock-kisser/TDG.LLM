using Microsoft.Extensions.DependencyInjection;
using Radzen;
using TDG.LLM.Client;
using TDG.LLM.WebApp.Components;

namespace TDG.LLM.WebApp
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			string geminiApiKey = builder.Configuration["GeminiApiKey"] ?? string.Empty;

			// Api Base URL aus Konfiguration
			var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
			if (string.IsNullOrWhiteSpace(apiBaseUrl))
			{
				throw new InvalidOperationException("ApiBaseUrl ist nicht konfiguriert. Füge sie zu appsettings.json oder Environment Variables hinzu.");
			}

			// Blazor + Radzen
			builder.Services.AddRazorPages();
			builder.Services.AddServerSideBlazor();
			builder.Services.AddRadzenComponents();

			// Konfig Service
			builder.Services.AddSingleton(new ApiUrlConfig(apiBaseUrl));

			// Typed HttpClient für ApiClient (DI-fähiger Konstruktor)
			builder.Services.AddHttpClient<ApiClient>((sp, client) =>
			{
				var cfg = sp.GetRequiredService<ApiUrlConfig>();
				client.BaseAddress = new Uri(cfg.BaseUrl);
			});

			// Gemini Client with API Key from config
			builder.Services.AddSingleton(new GeminiApiClient(geminiApiKey));

			var app = builder.Build();

			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();
			app.UseRouting();
			app.UseAntiforgery();

			app.MapBlazorHub();
			app.MapFallbackToPage("/_Host");
			app.MapRazorPages();

			app.Run();
		}
	}

	public class ApiUrlConfig
	{
		public string BaseUrl { get; set; }

		public ApiUrlConfig(string baseUrl)
		{
			this.BaseUrl = baseUrl;
		}
	}
}
