using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TDG.LLM.Core;

namespace TDG.LLM.Shared
{
	public class ImageData
	{
		public Guid Id { get; set; } = Guid.Empty;
		public int FrameId { get; set; } = 0;
		public int Width { get; set; } = 0;
		public int Height { get; set; } = 0;
		public string Format { get; set; } = string.Empty;

		public string Base64String { get; set; } = string.Empty;



		public ImageData()
		{
			// Parameterless constructor for deserialization
		}

		[JsonConstructor]
		public ImageData(ImageObj? obj = null, int frameId = -1)
		{
			if (obj == null)
			{
				return;
			}

			if (frameId < 0)
			{
				return;
			}

			try
			{
				this.Id = obj.Id;
				this.FrameId = frameId;
				this.Width = obj.Sizes[frameId].Width;
				this.Height = obj.Sizes[frameId].Height;
				this.Format = "png";
				this.Base64String = obj.GetBase64String(frameId) ?? string.Empty;
			}
			catch (Exception)
			{
				// Ignore errors and leave properties with default values
			}
		}


	}
}
