using Microsoft.AspNetCore.Mvc;
using TDG.LLM.Core;
using TDG.LLM.Shared;

namespace TDG.LLM.Api.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class ImageController : ControllerBase
	{
		private ImageCollection imageCollection;

		public ImageController(ImageCollection imageCollection)
		{
			this.imageCollection = imageCollection;
		}

		[HttpGet("list")]
		[ProducesResponseType(typeof(IEnumerable<ImageObjInfo>), 200)]
		[ProducesResponseType(typeof(ProblemDetails), 500)]
		public async Task<ActionResult<IEnumerable<ImageObjInfo>>> GetListAsync()
		{
			try
			{
				var infos = await Task.Run(() =>
				{
					return this.imageCollection.Images.Values.Select(img => new ImageObjInfo(img));
				});

				return this.Ok(infos);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, new ProblemDetails
				{
					Title = "Error retrieving image list",
					Detail = ex.Message,
					Status = 500
				});
			}
		}

		[HttpPost("load")]
		[Consumes("multipart/form-data")]
		[ProducesResponseType(typeof(ImageObjInfo), 200)]
		[ProducesResponseType(typeof(ProblemDetails), 400)]
		[ProducesResponseType(typeof(ProblemDetails), 500)]
		public async Task<ActionResult<ImageObjInfo>> LoadAsync(IFormFile file)
		{
			try
			{
				if (file == null || file.Length == 0)
				{
					return this.BadRequest(new ProblemDetails
					{
						Title = "Invalid file",
						Detail = "No file was uploaded or the file is empty.",
						Status = 400
					});
				}

				// Save the uploaded file to a temporary location
				var tempFilePath = Path.GetTempFileName();
				using (var stream = System.IO.File.Create(tempFilePath))
				{
					await file.CopyToAsync(stream);
				}

				var info = await Task.Run(() =>
				{
					var imgObj = this.imageCollection.Import(tempFilePath);
					return new ImageObjInfo(imgObj);
				});

				return this.Ok(info);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, new ProblemDetails
				{
					Title = "Error loading image",
					Detail = ex.Message,
					Status = 500
				});
			}
		}

		[HttpDelete("delete/{id}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(typeof(ProblemDetails), 404)]
		[ProducesResponseType(typeof(ProblemDetails), 500)]
		public async Task<IActionResult> DeleteAsync(Guid id)
		{
			try
			{
				var success = await Task.Run(() => this.imageCollection.Delete(id));
				if (!success)
				{
					return this.NotFound(new ProblemDetails
					{
						Title = "Image not found",
						Detail = $"No image found with ID {id}.",
						Status = 404
					});
				}
				return this.Ok();
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, new ProblemDetails
				{
					Title = "Error deleting image",
					Detail = ex.Message,
					Status = 500
				});
			}
		}

		[HttpGet("data/{id}")]
	}
}
