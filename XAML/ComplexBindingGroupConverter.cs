using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Com.Xenthrax.WindowsDataVisualizer.XAML
{
	[ValueConversion(typeof(IEnumerable), typeof(ComplexBindingGroup))]
	internal sealed class ComplexBindingGroupConverter : IValueConverter
	{
		public static readonly ComplexBindingGroupConverter Instance = new ComplexBindingGroupConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			IEnumerable Value = value as IEnumerable;

			if (Value == null)
				throw new ArgumentNullException("value");

			Type ienumerable_1 = Value.GetType().GetInterface(typeof(IEnumerable<>).FullName);
			return new ComplexBindingGroup()
			{
				// If is IEnumerable<T> then T else object
				ElementType = (ienumerable_1 != null && ienumerable_1.IsGenericType)
					? ienumerable_1.GetGenericArguments()[0]
					: typeof(object),
				Items = Value,
				Parameter = parameter as string
			};
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			ComplexBindingGroup bindingGroup = value as ComplexBindingGroup;

			if (bindingGroup == null)
				throw new ArgumentNullException("value");

			return bindingGroup.Items;
		}
	}

	public class ComplexBindingGroup : DaisleyHarrison.WPF.ComplexDataTemplates.IBindingGroup
	{
		internal ComplexBindingGroup() { }

		public Type ElementType { get; set; }
		public IEnumerable Items { get; set; }
		public string Parameter { get; set; }
	}
}