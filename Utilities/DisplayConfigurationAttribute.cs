using System;

namespace Com.Xenthrax.WindowsDataVisualizer.Utilities
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class DisplayConfigurationAttribute : Attribute
	{
		public DisplayConfigurationAttribute()
		{
			this.Converter = null;
			this.EmitDefaultValue = true;
			this.Ignore = false;
			this.IgnoreCamelCase = false;
			this.Name = null;
		}

		public DisplayConfigurationAttribute(string Name)
		{
			this.Converter = null;
			this.EmitDefaultValue = true;
			this.Ignore = false;
			this.IgnoreCamelCase = false;
			this.Name = Name;
		}

		public string Converter { get; set; }
		public bool EmitDefaultValue { get; set; }
		public bool Ignore { get; set; }
		public bool IgnoreCamelCase { get; set; }
		public string Name { get; set; }
	}
}