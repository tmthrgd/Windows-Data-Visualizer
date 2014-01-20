using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Com.Xenthrax.WindowsDataVisualizer.XAML
{
	[ValueConversion(typeof(IEnumerable<>), typeof(int))]
	internal sealed class IEnumerableCountConverter : IValueConverter
	{
		public static readonly IEnumerableCountConverter Instance = new IEnumerableCountConverter();
		
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			IEnumerable<dynamic> enumerable = value as IEnumerable<dynamic>;
			return (enumerable == null)
				? 0
				: enumerable.Count();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}