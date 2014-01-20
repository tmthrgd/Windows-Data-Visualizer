using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
	public partial class InputDialog : Window
	{
		public InputDialog()
		{
			InitializeComponent();
		}

		#region Members
		public readonly static DependencyProperty CertificateProperty = DependencyProperty.Register("Certificate", typeof(X509Certificate2), typeof(InputDialog));
		public readonly static DependencyProperty InputFileProperty = DependencyProperty.Register("InputFile", typeof(string), typeof(InputDialog), new PropertyMetadata(null, new PropertyChangedCallback(InputDialog.InputFile_OnChange)));
		public readonly static DependencyProperty IsEncryptedProperty = DependencyProperty.Register("IsEncrypted", typeof(bool?), typeof(InputDialog), new PropertyMetadata(true, new PropertyChangedCallback(InputDialog.IsEncrypted_OnChange)));
		#endregion

		#region Properties
		public X509Certificate2 Certificate
		{
			get
			{
				return (X509Certificate2)this.GetValue(InputDialog.CertificateProperty);
			}
			private set
			{
				this.SetValue(InputDialog.CertificateProperty, value);
			}
		}

		public string InputFile
		{
			get
			{
				return (string)this.GetValue(InputDialog.InputFileProperty);
			}
			private set
			{
				this.SetValue(InputDialog.InputFileProperty, value);
			}
		}

		public bool? IsEncrypted
		{
			get
			{
				return (bool?)this.GetValue(InputDialog.IsEncryptedProperty);
			}
			private set
			{
				this.SetValue(InputDialog.IsEncryptedProperty, value);
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

			if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.InputLastDataFile))
				this.InputFile = Properties.Settings.Default.InputLastDataFile;

			X509Store Store = null;

			try
			{
				Store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
				Store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

				this.Certificates.ItemsSource = Store.Certificates;

				if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.InputLastCertificate))
				{
					string[] LastCertificate = Properties.Settings.Default.InputLastCertificate.Split(':');

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

			if (string.IsNullOrEmpty(this.InputFile))
				this.InputFileBox.Focus();
			else if (this.Certificate == null && this.IsEncrypted == true)
				this.Certificates.Focus();
			else
				this.OK.Focus();
		}

		private void InputFileBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
				this.ShowFileDialog();
		}

		private void InputFileBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			this.ShowFileDialog();
		}

		private void ShowFileDialog()
		{
			OpenFileDialog FileDialog = new OpenFileDialog()
			{
				FileName = this.InputFile,
				Filter = "GZip|*.gz|JSON Files|*.json;*.js|BSON Files|*.bson|XML Files|*.xml|All Files|*.*"
			};

			if (!string.IsNullOrEmpty(FileDialog.FileName))
				FileDialog.DefaultExt = "." + Path.GetExtension(FileDialog.FileName);

			switch (FileDialog.DefaultExt)
			{
				case "gz":
				case ".gz":
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
				this.InputFile = Path.GetFileName(FileDialog.FileName);
			else
				this.InputFile = FileDialog.FileName;
		}

		private void CertificateView_Click(object sender, RoutedEventArgs e)
		{
			X509Certificate2 Certificate = this.Certificates.SelectedValue as X509Certificate2;

			if (Certificate != null)
				X509Certificate2UI.DisplayCertificate(Certificate, new WindowInteropHelper(this).EnsureHandle());
		}

		private void OK_Click(object sender, RoutedEventArgs e)
		{
			this.InputFileBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
			this.Certificates.GetBindingExpression(ComboBox.SelectedValueProperty).UpdateSource();

			if (Validation.GetHasError(this.InputFileBox))
				this.InputFileBox.Focus();
			else if (Validation.GetHasError(this.Certificates))
				this.Certificates.Focus();
			else
			{
				Properties.Settings.Default.InputLastDataFile = this.InputFile;

				if (this.IsEncrypted == true)
					Properties.Settings.Default.InputLastCertificate = string.Format("{0}:{1}", this.Certificate.SerialNumber, this.Certificate.Thumbprint);

				Properties.Settings.Default.Save();

				this.DialogResult = true;
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}

		private static void InputFile_OnChange(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			InputDialog Dialog = (InputDialog)sender;

			try
			{
				string FileName = Path.GetFullPath(e.NewValue as string);

				if (string.IsNullOrWhiteSpace(e.OldValue as string) || string.Compare(FileName, Path.GetFullPath(e.OldValue as string), true) != 0)
					using (FileStream FS = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
						Dialog.IsEncrypted = new bool?(Serializer.DataSerializer.IsEncrypted(FS));
			}
			catch
			{
				Dialog.IsEncrypted = null;
			}
		}

		private static void IsEncrypted_OnChange(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			InputDialog Dialog = (InputDialog)sender;

			Dialog.CertificateValidation.IsEnabled = ((bool?)e.NewValue == true);
			BindingExpression Binding = Dialog.Certificates.GetBindingExpression(ComboBox.SelectedValueProperty);

			if (Binding.HasError)
				Binding.UpdateSource();
		}
		#endregion
	}
}