using System;
using System.IO;
using System.Windows.Controls;

namespace Com.Xenthrax.WindowsDataVisualizer.XAML
{
	internal sealed class DataStealerFileValidationRule : ValidationRule
	{
		public static readonly DataStealerFileValidationRule Instance = new DataStealerFileValidationRule();

		public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
		{
			string FileName = value as string;

			if (string.IsNullOrWhiteSpace(FileName))
				return new ValidationResult(false, "Path cannot be null or empty.");

			try
			{
				FileName = Path.GetFullPath(FileName);
			}
			catch (Exception e)
			{
				return new ValidationResult(false, e.Message);
			}

			if (!File.Exists(FileName))
				return new ValidationResult(false, "Path must point to a valid file.");

			try
			{
				using (FileStream FS = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
					if (!Serializer.DataSerializer.IsValid(FS))
						return new ValidationResult(false, "Path must point to a valid DataStealer file.");
			}
			catch (Exception e)
			{
				return new ValidationResult(false, e.Message);
			}

			return new ValidationResult(true, null);
		}
	}
}