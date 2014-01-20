using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Com.Xenthrax.WindowsDataVisualizer
{
	public partial class SaveDialog : Window
	{
		public SaveDialog()
		{
			InitializeComponent();
		}

		#region Members
		public readonly static DependencyProperty CertificateProperty = DependencyProperty.Register("Certificate", typeof(X509Certificate2), typeof(SaveDialog));
		public readonly static DependencyProperty OutputFileProperty = DependencyProperty.Register("OutputFile", typeof(string), typeof(SaveDialog));
		public readonly static DependencyProperty EncryptionTypeProperty = DependencyProperty.Register("EncryptionType", typeof(Serializer.DataEncryptionType), typeof(SaveDialog), new PropertyMetadata(Serializer.DataSerializer.DefaultEncryptionType, new PropertyChangedCallback(SaveDialog.EncryptionType_OnChange)));
		public readonly static DependencyProperty KeySizeProperty = DependencyProperty.Register("KeySize", typeof(int), typeof(SaveDialog));
		public readonly static DependencyProperty BlockSizeProperty = DependencyProperty.Register("BlockSize", typeof(int), typeof(SaveDialog));
		public readonly static DependencyProperty FileFormatProperty = DependencyProperty.Register("FileFormat", typeof(Serializer.FileFormat), typeof(SaveDialog), new PropertyMetadata(Serializer.DataSerializer.DefaultFormat, new PropertyChangedCallback(SaveDialog.FileFormat_OnChange)));
		#endregion

		#region Properties
		public X509Certificate2 Certificate
		{
			get
			{
				return (X509Certificate2)this.GetValue(SaveDialog.CertificateProperty);
			}
			private set
			{
				this.SetValue(SaveDialog.CertificateProperty, value);
			}
		}

		public string OutputFile
		{
			get
			{
				return (string)this.GetValue(SaveDialog.OutputFileProperty);
			}
			private set
			{
				this.SetValue(SaveDialog.OutputFileProperty, value);
			}
		}

		public Serializer.DataEncryptionType EncryptionType
		{
			get
			{
				return (Serializer.DataEncryptionType)this.GetValue(SaveDialog.EncryptionTypeProperty);
			}
			private set
			{
				this.SetValue(SaveDialog.EncryptionTypeProperty, value);
			}
		}

		public int KeySize
		{
			get
			{
				return (int)this.GetValue(SaveDialog.KeySizeProperty);
			}
			private set
			{
				this.SetValue(SaveDialog.KeySizeProperty, value);
			}
		}

		public int BlockSize
		{
			get
			{
				return (int)this.GetValue(SaveDialog.BlockSizeProperty);
			}
			private set
			{
				this.SetValue(SaveDialog.BlockSizeProperty, value);
			}
		}

		public Serializer.FileFormat FileFormat
		{
			get
			{
				return (Serializer.FileFormat)this.GetValue(SaveDialog.FileFormatProperty);
			}
			private set
			{
				this.SetValue(SaveDialog.FileFormatProperty, value);
			}
		}
		#endregion

		#region Methods
		public bool? ShowDialog(Window owner)
		{
			this.Owner = owner;
			return this.ShowDialog();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.Reload();

			if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.SaveLastDataFile))
				this.OutputFile = Properties.Settings.Default.SaveLastDataFile;
			
			Serializer.DataEncryptionType EncryptionType;

			if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.SaveLastEncryptionType)
				&& Enum.TryParse(Properties.Settings.Default.SaveLastEncryptionType, out EncryptionType))
				this.EncryptionType = EncryptionType;

			try
			{
				Guid FormatIdentifier;

				if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.SaveLastFormat)
					&& Guid.TryParse(Properties.Settings.Default.SaveLastFormat, out FormatIdentifier))
					this.FileFormat = Serializer.DataSerializer.GetFileFormatFromIdentifier(FormatIdentifier);
			}
			catch (Exception ex)
			{
				Utilities.Utilities.Log(ex);
			}

			X509Store Store = null;

			try
			{
				Store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
				Store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

				this.Certificates.ItemsSource = Store.Certificates;

				if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.SaveLastCertificate))
				{
					string[] LastCertificate = Properties.Settings.Default.SaveLastCertificate.Split(':');

					if (LastCertificate.Length == 2)
					{
						X509Certificate2Collection MatchingCerts = Store.Certificates
							.Find(X509FindType.FindBySerialNumber, LastCertificate[0], false)
							.Find(X509FindType.FindByThumbprint, LastCertificate[1], false);

						if (MatchingCerts.Count > 0)
							this.Certificate = MatchingCerts[0];
					}
				}
			}
			catch (Exception ex)
			{
				Utilities.Utilities.Log(ex);
			}
			finally
			{
				if (Store != null)
					Store.Close();
			}

			this.Formats.ItemsSource = Serializer.DataSerializer.FileFormats;
			this.EncryptionTypes.ItemsSource = Enum.GetValues(typeof(Serializer.DataEncryptionType));

			SaveDialog.FileFormat_OnChange(this, new DependencyPropertyChangedEventArgs(SaveDialog.EncryptionTypeProperty, Serializer.DataSerializer.DefaultFormat, this.FileFormat));
			SaveDialog.EncryptionType_OnChange(this, new DependencyPropertyChangedEventArgs(SaveDialog.EncryptionTypeProperty, Serializer.DataSerializer.DefaultEncryptionType, this.EncryptionType));

			if (string.IsNullOrEmpty(this.OutputFile))
				this.OutputFileBox.Focus();
			else
				this.OK.Focus();
		}

		private void OutputFileBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
				this.ShowFileDialog();
		}

		private void OutputFileBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			this.ShowFileDialog();
		}

		private void ShowFileDialog()
		{
			SaveFileDialog FileDialog = new SaveFileDialog()
			{
				FileName = this.OutputFile,
				Filter = "GZip|*.gz|JSON Files|*.json;*.js|BSON Files|*.bson|XML Files|*.xml|All Files|*.*"
			};
			
			if (string.IsNullOrEmpty(FileDialog.FileName))
			{
				if (this.FileFormat == Serializer.Formats.Bson)
					FileDialog.DefaultExt = ".bson";
				else if (this.FileFormat == Serializer.Formats.Json)
					FileDialog.DefaultExt = ".json";
				else if (this.FileFormat == Serializer.Formats.XML)
					FileDialog.DefaultExt = ".xml";
				else if (this.FileFormat == Serializer.Formats.CompressedBson)
					FileDialog.DefaultExt = ".bson.gz";
				else if (this.FileFormat == Serializer.Formats.CompressedJson)
					FileDialog.DefaultExt = ".json.gz";
				else if (this.FileFormat == Serializer.Formats.CompressedXML)
					FileDialog.DefaultExt = ".xml.gz";
			}
			else
				FileDialog.DefaultExt = "." + Path.GetExtension(FileDialog.FileName);

			switch (FileDialog.DefaultExt)
			{
				case "gz":
				case ".gz":
				case "bson.gz":
				case ".bson.gz":
				case "json.gz":
				case ".json.gz":
				case "xml.gz":
				case ".xml.gz":
					FileDialog.FilterIndex = 1;
					break;
				case "json":
				case ".json":
				case "js":
				case ".js":
					FileDialog.FilterIndex = 2;
					break;
				case "bson":
				case ".bson":
					FileDialog.FilterIndex = 3;
					break;
				case "xml":
				case ".xml":
					FileDialog.FilterIndex = 4;
					break;
				default:
					FileDialog.FilterIndex = 5;
					break;
			}

			if (FileDialog.ShowDialog(this) != true)
				return;

			if (Path.GetDirectoryName(FileDialog.FileName).ToLower()
				+ Path.DirectorySeparatorChar == AppDomain.CurrentDomain.BaseDirectory.ToLower())
				this.OutputFile = Path.GetFileName(FileDialog.FileName);
			else
				this.OutputFile = FileDialog.FileName;
		}

		private void CertificateView_Click(object sender, RoutedEventArgs e)
		{
			X509Certificate2 Certificate = this.Certificates.SelectedValue as X509Certificate2;

			if (Certificate != null)
				X509Certificate2UI.DisplayCertificate(Certificate, new WindowInteropHelper(this).EnsureHandle());
		}

		private void OK_Click(object sender, RoutedEventArgs e)
		{
			this.OutputFileBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
			this.Certificates.GetBindingExpression(ComboBox.SelectedValueProperty).UpdateSource();

			if (Validation.GetHasError(this.OutputFileBox))
				this.OutputFileBox.Focus();
			else if (Validation.GetHasError(this.Certificates))
				this.Certificates.Focus();
			else
			{
				Properties.Settings.Default.SaveLastDataFile = this.OutputFile;
				Properties.Settings.Default.SaveLastEncryptionType = this.EncryptionType.ToString();
				Properties.Settings.Default.SaveLastFormat = this.FileFormat.Identifier.ToString();

				if (this.EncryptionType != Serializer.DataEncryptionType.NoEncryption)
					Properties.Settings.Default.SaveLastCertificate = string.Format("{0}:{1}", this.Certificate.SerialNumber, this.Certificate.Thumbprint);

				Properties.Settings.Default.Save();

				this.DialogResult = true;
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}

		private static void FileFormat_OnChange(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			SaveDialog Dialog = (SaveDialog)sender;

			if (!((Serializer.FileFormat)e.NewValue).SupportsEncryption)
			{
				Dialog.EncryptionType = Serializer.DataEncryptionType.NoEncryption;
				Dialog.EncryptionTypes.IsEnabled = false;
			}
			else
				Dialog.EncryptionTypes.IsEnabled = true;
		}

		private static void EncryptionType_OnChange(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			SaveDialog Dialog = (SaveDialog)sender;
			Serializer.DataEncryptionType EncryptionType = (Serializer.DataEncryptionType)e.NewValue;

			Dialog.CertificateValidation.IsEnabled = (EncryptionType != Serializer.DataEncryptionType.NoEncryption);
			
			Dialog.BlockSize = Serializer.DataSerializer.GetStrongestBlockSize(EncryptionType);
			Dialog.KeySize = Serializer.DataSerializer.GetStrongestKeySize(EncryptionType);

			BindingExpression Binding = Dialog.Certificates.GetBindingExpression(ComboBox.SelectedValueProperty);

			if (Binding.HasError)
				Binding.UpdateSource();
		}
		#endregion
	}
}