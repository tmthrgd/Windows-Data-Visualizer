using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Com.Xenthrax.WindowsDataVisualizer.XAML
{
	[ValueConversion(typeof(string), typeof(string))]
	internal sealed class TextWordAfterConverter : IValueConverter
	{
		public static readonly TextWordAfterConverter Instance = new TextWordAfterConverter();

		private static char[] Punctuation = new char[0];

		static TextWordAfterConverter()
		{
			List<char> Punctuation = new List<char>();

			for (char c = char.MinValue; c < char.MaxValue; c++)
				if (char.IsPunctuation(c) || char.IsWhiteSpace(c))
					Punctuation.Add(c);
			
			TextWordAfterConverter.Punctuation = Punctuation.ToArray();
		}

		public string Convert(string Value, int MaxLength)
		{
			return this.Convert(Value, MaxLength, "...");
		}

		public string Convert(string Value, int MaxLength, string OverflowText)
		{
			if (Value == null || MaxLength < 0 || OverflowText == null)
				return null;

			int index;
			return (Value.Length > MaxLength
				&& (index = Value.IndexOfAny(TextWordAfterConverter.Punctuation, MaxLength)) != -1
				&& index < Value.Length)
				? Value.Substring(0, index) + OverflowText
				: Value;
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (parameter == null)
				return null;

			string[] Parameter = parameter.ToString().Split(new char[] { ',' }, 2);

			if (Parameter.Length == 0)
				return null;
			else if (Parameter.Length > 1)
				return this.Convert(value as string, int.Parse(Parameter[0]), Parameter[1]);
			else
				return this.Convert(value as string, int.Parse(Parameter[0]));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}