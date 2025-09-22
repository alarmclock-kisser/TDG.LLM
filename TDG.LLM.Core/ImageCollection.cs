using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TDG.LLM.Core
{
	public class ImageCollection : IDisposable
	{
		public ConcurrentDictionary<Guid, ImageObj> Images { get; set; } = [];


		public ImageCollection()
		{

		}





		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}

		public ImageObj? Import(string filePath)
		{
			try
			{
				var img = new ImageObj(filePath);
				if (this.Images.TryAdd(img.Id, img))
				{
					return img;
				}
				else
				{
					img.Dispose();
					return null;
				}
			}
			catch
			{
				return null;
			}
		}

		public bool Delete(Guid id)
		{
			if (this.Images.TryRemove(id, out var img))
			{
				img.Dispose();
				return true;
			}
			else
			{
				return false;
			}
		}

		public void Clear()
		{
			foreach (var img in this.Images.Values)
			{
				img.Dispose();
			}

			this.Images.Clear();
		}
	}
}
