using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TDG.LLM.Core;

namespace TDG.LLM.Shared
{
	public class ImageObjInfo
	{
		public Guid Id { get; set; } = Guid.Empty;
		public string FilePath { get; set; } = string.Empty;
		public int FrameCount { get; set; } = 0;

		public ImageObjInfo()
		{
			// Empty constructor for serialization
		}

		[JsonConstructor]
		public ImageObjInfo(ImageObj? obj)
		{
			if (obj == null)
			{
				return;
			}

			this.Id = obj.Id;
			this.FilePath = obj.FilePath;
			this.FrameCount = obj.Frames.Length;
		}



	}
}
