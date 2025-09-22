using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDG.LLM.Core
{
	public class ImageObj : IDisposable
	{
		public Guid Id { get; private set; } = Guid.NewGuid();
		public string FilePath { get; private set; } = string.Empty;

		public Image<Rgba32>[] Frames { get; private set; } = [];
		public Size[] Sizes { get; private set; } = [];

		public int Channels => 4;
		public int BitsPerChannel => 8;
		public int BitsPerPixel => this.Channels * this.BitsPerChannel;

		public bool OnHost { get; set; } = false;
		public bool OnDevice { get; set; } = false;
		public IntPtr Pointer { get; set; } = IntPtr.Zero;



		public ImageObj(string filePath)
		{
			// Load image(s) from file path
			this.FilePath = filePath;
			using var image = Image.Load(filePath);

			if (image.Frames.Count > 1)
			{
				// Multi frame image
				this.Frames = new Image<Rgba32>[image.Frames.Count];
				this.Sizes = new Size[image.Frames.Count];
				for (int i = 0; i < image.Frames.Count; i++)
				{
					var frame = image.Frames.CloneFrame(i);
					this.Frames[i] = frame.CloneAs<Rgba32>();
					this.Sizes[i] = frame.Size;
				}
			}
			else
			{
				// Static image
				var singleFrame = image.CloneAs<Rgba32>();
				this.Frames = [singleFrame];
				this.Sizes = [singleFrame.Size];
			}
		}



		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}

		public byte[] GetPixels(int frameId = 0)
		{
			if (frameId < 0 || frameId >= this.Frames.Length)
			{
				return [];
			}

			var frame = this.Frames[frameId];
			var pixelBytes = new byte[frame.Width * frame.Height * 4];
			frame.CopyPixelDataTo(pixelBytes);

			return pixelBytes;
		}

		public Image<Rgba32>? SetPixels(byte[] pixels, int frameId = 0)
		{
			if (frameId < 0 || frameId >= this.Frames.Length)
			{
				return null;
			}

			var frame = this.Frames[frameId];
			var size = frame.Size;

			if (pixels.Length != frame.Width * frame.Height * 4)
			{
				return null;
			}

			frame.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < accessor.Height; y++)
				{
					var pixelRow = accessor.GetRowSpan(y);
					for (int x = 0; x < accessor.Width; x++)
					{
						int index = (y * size.Width + x) * this.Channels;
						pixelRow[x] = new Rgba32(
							pixels[index],     // R
							pixels[index + 1], // G
							pixels[index + 2], // B
							pixels[index + 3]  // A
						);
					}
				}
			});

			this.Frames[frameId] = frame;

			return frame;
		}

		public string? GetBase64String(int frameId = 0)
		{
			if (frameId < 0 || frameId >= this.Frames.Length)
			{
				return null;
			}

			var frame = this.Frames[frameId];
			using var ms = new MemoryStream();
			frame.SaveAsPng(ms);
			var base64String = Convert.ToBase64String(ms.ToArray());

			return base64String;
		}
	}
}
