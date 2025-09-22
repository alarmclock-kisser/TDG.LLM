
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using TDG.LLM.Core;

namespace TDG.LLM.Api
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			bool swaggerEnabled = builder.Configuration.GetValue<bool>("SwaggerEnabled");
			int maxUploadSize = builder.Configuration.GetValue<int>("MaxUploadSizeMb") * 1_000_000;

			// Add services to the container.
			builder.Services.AddSingleton<ImageCollection>();

			// Swagger/OpenAPI
			builder.Services.AddEndpointsApiExplorer();
			if (swaggerEnabled)
			{
				// Show full Swagger UI with endpoints
				builder.Services.AddSwaggerGen();
			}
			else
			{
				builder.Services.AddSwaggerGen(c =>
				{
					c.SwaggerDoc("v1", new OpenApiInfo
					{
						Version = "v1",
						Title = "APICL",
						Description = "API + WebApp using OpenCL for media manipulation",
						TermsOfService = new Uri("https://localhost:7116/terms"),
						Contact = new OpenApiContact { Name = "Developer", Email = "marcel.king91299@gmail.com" }
					});

					c.AddServer(new OpenApiServer { Url = "https://localhost:5115" });
					c.DocInclusionPredicate((_, api) => !string.IsNullOrWhiteSpace(api.GroupName));
					c.TagActionsBy(api => [api.GroupName ?? "Default"]);
				});
			}

			// Request Body Size Limits
			builder.WebHost.ConfigureKestrel(options =>
			{
				options.Limits.MaxRequestBodySize = maxUploadSize;
			});

			builder.Services.Configure<IISServerOptions>(options =>
			{
				options.MaxRequestBodySize = maxUploadSize;
			});

			builder.Services.Configure<FormOptions>(options =>
			{
				options.MultipartBodyLengthLimit = maxUploadSize;
			});

			// Logging
			builder.Logging.AddConsole();
			builder.Logging.AddDebug();
			builder.Logging.SetMinimumLevel(LogLevel.Debug);


			builder.Services.AddControllers();
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();

			// CORS policy
			builder.Services.AddCors(options =>
			{
				options.AddPolicy("BlazorCors", policy =>
				{
					policy.WithOrigins("https://localhost:7254")
						  .AllowAnyHeader()
						  .AllowAnyMethod();
				});
			});

			var app = builder.Build();

			// Development-only Middlewares
			if (app.Environment.IsDevelopment())
			{
				if (app.Environment.IsDevelopment())
				{
					app.UseSwagger();

					if (swaggerEnabled)
					{
						// Show endpoints
						app.UseSwaggerUI();
					}
					else
					{
						// Show only info page about the API
						app.UseSwaggerUI(c =>
						{
							c.SwaggerEndpoint("/swagger/v1/swagger.json", "APICL v1.0");
						});
					}
				}
			}

			app.UseStaticFiles();
			app.UseHttpsRedirection();
			app.UseCors("BlazorCors");
			app.MapControllers();

			app.Run();

		}
	}
}
