using System;
using System.Collections.Generic;

namespace Com.Xenthrax.WindowsDataVisualizer.Utilities
{
	internal class NoThrowDictionary<TKey, TValue> : Dictionary<TKey, TValue>
	{
		public new TValue this[TKey key]
		{
			get
			{
				return base.ContainsKey(key)
					? base[key]
					: base[key] = default(TValue);
			}
			set
			{
				base[key] = value;
			}
		}
	}
}