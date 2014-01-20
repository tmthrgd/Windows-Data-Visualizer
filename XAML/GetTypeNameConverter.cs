using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Com.Xenthrax.WindowsDataVisualizer.XAML
{
	[ValueConversion(typeof(object), typeof(string))]
	internal sealed class GetTypeNameConverter : IValueConverter
	{
		public static readonly GetTypeNameConverter Instance = new GetTypeNameConverter();

		public GetTypeNameConverter()
		{
			this.Fullname = false;
		}

		public bool Fullname { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			if (this.Fullname)
				return value.GetType().FullName;
			else
				return value.GetType().Name;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}