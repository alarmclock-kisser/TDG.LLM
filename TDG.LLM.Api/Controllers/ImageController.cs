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

				 // Verwende den Original-Dateinamen (bereinigt) anstelle eines generischen Temp-Namens
				var originalFileName = Path.GetFileName(file.FileName); // entfernt etwaige Pfadangaben
				var invalidChars = Path.GetInvalidFileNameChars();
				var safeFileName = string.Join("_", originalFileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
				if (string.IsNullOrWhiteSpace(safeFileName))
				{
					safeFileName = "upload";
				}

				// Stelle sicher, dass eine Erweiterung erhalten bleibt (falls vorhanden)
				var extension = Path.GetExtension(safeFileName);
				if (string.IsNullOrEmpty(extension))
				{
					var origExt = Path.GetExtension(originalFileName);
					if (!string.IsNullOrEmpty(origExt))
					{
						safeFileName += origExt;
					}
				}

				var tempDir = Path.GetTempPath();
				var destPath = Path.Combine(tempDir, safeFileName);

				// Kollision vermeiden
				if (System.IO.File.Exists(destPath))
				{
					var baseName = Path.GetFileNameWithoutExtension(safeFileName);
					var ext = Path.GetExtension(safeFileName);
					destPath = Path.Combine(tempDir, $"{baseName}_{Guid.NewGuid():N}{ext}");
				}

				using (var stream = System.IO.File.Create(destPath))
				{
					await file.CopyToAsync(stream);
				}

				var info = await Task.Run(() =>
				{
					var imgObj = this.imageCollection.Import(destPath);
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

		[HttpDelete("clear")]
		[ProducesResponseType(200)]
		[ProducesResponseType(typeof(ProblemDetails), 500)]
		public async Task<IActionResult> ClearAsync()
		{
			try
			{
				await Task.Run(this.imageCollection.Clear);
				return this.Ok();
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, new ProblemDetails
				{
					Title = "Error clearing images",
					Detail = ex.Message,
					Status = 500
				});
			}
		}

		[HttpGet("data/{id}/{frame}")]
		[ProducesResponseType(typeof(ImageData), 200)]
		[ProducesResponseType(typeof(ProblemDetails), 404)]
		[ProducesResponseType(typeof(ProblemDetails), 500)]
		public async Task<ActionResult<ImageData>> GetImageDataAsync(Guid id, int frame = 0)
		{
			try
			{
				var obj = this.imageCollection.Images.GetValueOrDefault(id);
				if (obj == null)
				{
					return this.NotFound(new ProblemDetails
					{
						Title = "Image not found",
						Detail = $"No image found with ID {id}.",
						Status = 404
					});
				}

				if (frame < 0 || frame >= obj.Frames.Length)
				{
					return this.NotFound(new ProblemDetails
					{
						Title = "Frame not found",
						Detail = $"No frame {frame} found for image ID {id}.",
						Status = 404
					});
				}

				var imgData = await Task.Run(() => new ImageData(obj, frame));
				if (imgData == null)
				{
					return this.NotFound(new ProblemDetails
					{
						Title = "Image not found",
						Detail = $"No image found with ID {id}.",
						Status = 404
					});
				}

				return this.Ok(imgData);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, new ProblemDetails
				{
					Title = "Error retrieving image data",
					Detail = ex.Message,
					Status = 500
				});
			}
		}
	}
}
