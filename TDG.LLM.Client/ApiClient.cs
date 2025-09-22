using Microsoft.AspNetCore.Http.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TDG.LLM.Shared;

namespace TDG.LLM.Client
{
	public class ApiClient
	{
		private readonly InternalClient internalClient;

		public ApiClient(string baseUrl = "https://localhost:7230")
		{
			HttpClient httpClient = new()
			{
				BaseAddress = new Uri(baseUrl)
			};

			this.internalClient = new InternalClient(baseUrl, httpClient);
		}

		public async Task<IEnumerable<TDG.LLM.Shared.ImageObjInfo>> GetImageListAsync()
		{
			var response = new List<TDG.LLM.Shared.ImageObjInfo>();

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

		public async Task<ImageObjInfo> UploadImage(FileParameter file)
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

		public async Task DeleteImage(Guid id)
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
	}
}
