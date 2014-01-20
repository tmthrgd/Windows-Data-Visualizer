using System;

namespace Com.Xenthrax.WindowsDataVisualizer.Utilities
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class NavigateToAttribute : Attribute { }
}