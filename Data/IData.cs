using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	public interface IData<TInitiate> : IData
		where TInitiate : IData<TInitiate>
	{
		TInitiate Initiate();
	}

	public interface IData { }
}