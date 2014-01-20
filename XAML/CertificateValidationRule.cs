using System;
using System.Windows.Controls;
using System.Security.Cryptography.X509Certificates;

namespace Com.Xenthrax.WindowsDataVisualizer.XAML
{
	internal sealed class CertificateValidationRule : ValidationRule
	{
		public static readonly CertificateValidationRule Instance = new CertificateValidationRule();

		public CertificateValidationRule()
		{
			this.IsEnabled = true;
			this.Verify = true;
			this.NeedsPrivateKey = false;
		}

		public bool IsEnabled { get; set; }
		public bool Verify { get; set; }
		public bool NeedsPrivateKey { get; set; }

		public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
		{
			if (!this.IsEnabled)
				return new ValidationResult(true, null);

			X509Certificate2 Certificate = value as X509Certificate2;

			if (Certificate == null)
				return new ValidationResult(false, "A certificate must be selected.");

			if (this.Verify && !Certificate.Verify())
				return new ValidationResult(false, "Certificate is not valid.");

			if (this.NeedsPrivateKey && !Certificate.HasPrivateKey)
				return new ValidationResult(false, "Certificate must have a private key.");

			return new ValidationResult(true, null);
		}
	}
}