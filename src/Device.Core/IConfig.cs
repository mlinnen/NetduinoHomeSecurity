using System;
using Microsoft.SPOT;

namespace Device.Core
{
	public interface IConfig
	{
		void Load(string fileName);
	}
}
