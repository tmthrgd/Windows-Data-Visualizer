using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Com.Xenthrax.WindowsDataVisualizer
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			this.Exceptions = new System.Collections.ObjectModel.ObservableCollection<Exception>();
			Utilities.Utilities.ExceptionHandler += this.Utilities_ExceptionHandler;

			InitializeComponent();
		}

		#region Members
		private readonly static DependencyProperty ExceptionsProperty = DependencyProperty.Register("Exceptions", typeof(ICollection<Exception>), typeof(MainWindow));
		private readonly static DependencyProperty DataProperty = DependencyProperty.Register("Data", typeof(Data.Data), typeof(MainWindow));
		#endregion

		#region Properties
		private ICollection<Exception> Exceptions
		{
			get
			{
				return (ICollection<Exception>)this.GetValue(MainWindow.ExceptionsProperty);
			}
			set
			{
				this.SetValue(MainWindow.ExceptionsProperty, value);
			}
		}
		
		private Data.Data Data
		{
			get
			{
				return (Data.Data)this.GetValue(MainWindow.DataProperty);
			}
			set
			{
				this.SetValue(MainWindow.DataProperty, value);
			}
		}
		#endregion

		#region Methods
		private void Utilities_ExceptionHandler(object sender, Utilities.ExceptionEventArgs e)
		{
			this.Dispatcher.Invoke(new Action<Exception>((ex) => this.Exceptions.Add(ex)), e.Exception);
			e.Handled = true;
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			Utilities.Utilities.CleanUpLog();
		}

		private void New_CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				this.Data = new Data.Data().Initiate();
				this.DataTree_SelectedItemChanged(this, new RoutedPropertyChangedEventArgs<object>(this.DataTree.SelectedItem, this.DataTree.SelectedItem));
			}
			catch (Exception ex)
			{
				Utilities.Utilities.Log(ex);
			}
		}

		private void Open_CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				InputDialog Input = new InputDialog();

				if (Input.ShowDialog(this) != true)
					return;

				using (FileStream FS = new FileStream(Input.InputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					this.Data = Serializer.DataSerializer.Deserialize(FS, Input.Certificate);
					this.DataTree_SelectedItemChanged(this, new RoutedPropertyChangedEventArgs<object>(this.DataTree.SelectedItem, this.DataTree.SelectedItem));
				}
			}
			catch (Exception ex)
			{
				Utilities.Utilities.Log(ex);
			}
		}

		private void Save_CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				SaveDialog Save = new SaveDialog();

				if (Save.ShowDialog(this) != true)
					return;

				using (FileStream FS = new FileStream(Save.OutputFile, FileMode.Create, FileAccess.Write, FileShare.Read))
					Serializer.DataSerializer.Serialize(this.Data, FS, Save.EncryptionType, Save.Certificate, Save.KeySize, Save.BlockSize, Save.FileFormat);
			}
			catch (Exception ex)
			{
				Utilities.Utilities.Log(ex);
			}
		}

		private void Close_CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.Close();
		}

		private void DataTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (e.NewValue == null)
				return;

			this.DataTextBox.Document = this.NewFlowDocument(new Run("Loading\u2026")
			{
				FontStyle = FontStyles.Italic
			});

			var worker = new System.ComponentModel.BackgroundWorker();
			worker.DoWork += (paramater, workerargs) =>
			{
				var NewValue = e.NewValue;
				
				if (NewValue is XAML.ComplexBindingGroup)
					NewValue = XAML.ComplexBindingGroupConverter.Instance.ConvertBack(NewValue, null, null, null);

				// Exception
				var exception = NewValue as Exception;

				if (exception != null)
				{
					StringBuilder ErrorString = new StringBuilder();
					
					while (exception != null)
					{
						ErrorString.AppendFormat("{0}\r\n\r\n", exception);
						exception = exception.InnerException;
					}

					this.Dispatcher.Invoke(new Action(() =>
					{
						this.DataTextBox.Document = this.NewFlowDocument(new Run(ErrorString.ToString().TrimEnd('\r', '\n')));
					}));
					return;
				}

				// IEnumerable<>
				var item = NewValue as FrameworkElement;
				IEnumerable<dynamic> enumerable = null;
				
				if (item != null)
					enumerable = this.Dispatcher.Invoke(new Func<object>(() => item.DataContext)) as IEnumerable<dynamic>;
				else
					enumerable = NewValue as IEnumerable<dynamic>;

				if (enumerable != null)
				{
					this.Dispatcher.Invoke(new Action(() =>
					{
						this.DataTextBox.Document = this.NewFlowDocument(new Run(this.IEnumerableToString(enumerable, 1000)));
					}));
					return;
				}

				// Data.IData
				Data.IData data = null; 
				
				if (item != null)
					data = this.Dispatcher.Invoke(new Func<object>(() => item.DataContext)) as Data.IData;
				else
					data = NewValue as Data.IData;

				if (data != null)
				{
					var Properties = new List<Inline>();
					Uri navigateTo = null;
					
					foreach (var prop in data.GetType().GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public))
					{
						try
						{
							if (prop.GetCustomAttributes(typeof(Utilities.NavigateToAttribute), false).Length != 0)
								navigateTo = prop.GetValue(data, null) as Uri;
						}
						catch (Exception ex)
						{
							Utilities.Utilities.Log(ex);
						}

						string name = null;
						object value;

						try
						{
							var dispAttr = (Utilities.DisplayConfigurationAttribute)prop.GetCustomAttributes(typeof(Utilities.DisplayConfigurationAttribute), false).LastOrDefault();
							bool valueIsInline = false;

							if (dispAttr == null)
							{
								name = Utilities.Utilities.FromCamelCase(prop.Name);
								value = prop.GetValue(data, null);
							}
							else
							{
								if (dispAttr.Ignore)
									continue;

								value = prop.GetValue(data, null);

								if (!dispAttr.EmitDefaultValue && value == prop.PropertyType.Default())
									continue;

								name = dispAttr.Name ?? (dispAttr.IgnoreCamelCase ? prop.Name : Utilities.Utilities.FromCamelCase(prop.Name));

								if (dispAttr.Converter != null)
								{
									var converter = prop.DeclaringType.GetMethod(dispAttr.Converter, BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[1] { prop.PropertyType }, null);

									if (converter == null)
										throw new MissingMethodException(prop.DeclaringType.FullName, dispAttr.Converter);
									else if (converter.ReturnType == typeof(Inline) || converter.ReturnType.IsSubclassOf(typeof(Inline)))
									{
										valueIsInline = true;
										value = this.Dispatcher.Invoke(new Func<object, object[], object>(converter.Invoke), data, new object[1] { value });
									}
									else
										value = converter.Invoke(data, new object[1] { value });
								}
							}

							if (value != null && !valueIsInline && !(value is string))
							{
								if (value is DirectoryInfo)
									value = ((DirectoryInfo)value).FullName;
								else if (value is FileInfo)
									value = ((FileInfo)value).FullName;
								else if (value is IEnumerable<dynamic>)
									value = this.IEnumerableToString((IEnumerable<dynamic>)value, 5);
								else if (value is DateTime)
									value = TimeZone.CurrentTimeZone.ToLocalTime((DateTime)value);
							}

							this.Dispatcher.Invoke(new Action(() =>
							{
								Properties.AddRange(
									new Run(string.Format("{0}: ", name)),
									valueIsInline
										? (Inline)value
										: new Run((value == null) ? string.Empty : value.ToString()),
									new Run("\r\n"));
							}));
						}
						catch (Exception ex)
						{
							Utilities.Utilities.Log(ex);

							this.Dispatcher.Invoke(new Action(() =>
							{
								Properties.AddRange(
									new Run(string.Format("{0}:\r\n", name ?? prop.Name)),
									new Run(string.Format("\t{0}", ex))
									{
										FontStyle = FontStyles.Italic
									},
									new Run("\r\n"));
							}));
						}
					}

					if (Properties.Count > 0)
						Properties.RemoveLast();

					this.Dispatcher.Invoke(new Action(() =>
					{
						if (navigateTo != null)
							this.DataTextBox.Document = this.NewFlowDocumentWithWebBrowser(navigateTo, Properties);
						else
							this.DataTextBox.Document = this.NewFlowDocument(Properties);
					}));

					return;
				}

				this.Dispatcher.Invoke(new Action(this.DataTextBox.Document.Blocks.Clear));
			};
			worker.RunWorkerAsync();
		}

		private string IEnumerableToString(IEnumerable<dynamic> value, int Limit = -1)
		{
			int Count = value.Count();
			
			if (Limit > 0 && Count > Limit)
				value = value.Take(Limit);

			if (value is IEnumerable<Exception>)
				value = value.Select(ex => string.Format("{0}: {1}", ex.GetType(), ex.Message));
			
			if (Limit <= 0 || Count <= Limit)
				return string.Format("{0}\r\n\t{1}", Count, string.Join("\r\n\t", value)).TrimEnd('\r', '\n', '\t');
			else
				return string.Format("{0}\r\n\t{1}\r\n\t\u2026", Count, string.Join("\r\n\t", value));
		}

		private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			if (e.Uri != null)
				System.Diagnostics.Process.Start(e.Uri.ToString());
		}
		
		private FlowDocument NewFlowDocument(params Inline[] Inlines)
		{
			return this.NewFlowDocument((IEnumerable<Inline>)Inlines);
		}

		private FlowDocument NewFlowDocument(IEnumerable<Inline> Inlines)
		{
			Paragraph Paragraph = new Paragraph();
			Paragraph.Inlines.AddRange(Inlines);
			return this.NewFlowDocument(Paragraph);
		}

		private FlowDocument NewFlowDocument(params Block[] Blocks)
		{
			FlowDocument Document = new FlowDocument();
			Document.Blocks.AddRange(Blocks);
			return Document;
		}

		private FlowDocument NewFlowDocumentWithWebBrowser(Uri WebBrowserUrl, params Inline[] Inlines)
		{
			return this.NewFlowDocumentWithWebBrowser(WebBrowserUrl, (IEnumerable<Inline>)Inlines);
		}

		private FlowDocument NewFlowDocumentWithWebBrowser(Uri WebBrowserUrl, IEnumerable<Inline> Inlines)
		{
			Paragraph Paragraph = new Paragraph();
			Paragraph.Inlines.AddRange(Inlines);
			return this.NewFlowDocumentWithWebBrowser(WebBrowserUrl, Paragraph);
		}

		private FlowDocument NewFlowDocumentWithWebBrowser(Uri WebBrowserUrl, params Block[] Blocks)
		{
			FlowDocument Document = this.NewFlowDocument(Blocks);

			try
			{
				WebBrowser Browser = new WebBrowser()
				{
					MinHeight = 450
				};
				Browser.Navigate(WebBrowserUrl);
				Document.Blocks.Add(new BlockUIContainer(Browser));

				Document.Unloaded += new RoutedEventHandler((sender, e) =>
				{
					Browser.Dispose();
					Browser = null;
				});
			}
			catch (Exception ex)
			{
				Utilities.Utilities.Log(ex);
			}

			return Document;
		}
		#endregion
	}
}