using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Xml;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class WindowsLiveMessenger : IData<WindowsLiveMessenger>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public WindowsLiveMessenger Initiate()
		{
			try
			{
				using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(WindowsLiveMessenger.MSNRegKey, false))
					if (rk != null)
						this.Profiles = rk.GetSubKeyNames().Select(profile => new Profile(profile).Initiate()).Memoize().AsSerializable();
					else
						this.Profiles = Enumerable.Empty<Profile>();
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				this.Profiles = Enumerable.Empty<Profile>();
			}

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Windows Live Messenger"
				: string.Format("Windows Live Messenger ({0})", this.Profiles.Count());
		}

		private string ProfilesConverter(IEnumerable<Profile> Data)
		{
			return string.Format("{0}\r\n\t{1}", Data.Count(), string.Join("\r\n\t", Data)).TrimEnd('\r', '\n', '\t');
		}

		#region Types
		[DataContract]
		public sealed class Profile : IData<Profile>
		{
			public Profile() { }

			public Profile(string ProfileID)
			{
				this.ProfileID = ProfileID;
			}

			[DataMember]
			public string ProfileID { get; private set; }

			[DataMember]
			public IEnumerable<Account> Accounts { get; private set; }
			[DataMember]
			public IEnumerable<History> History { get; private set; }

			public Profile Initiate()
			{
				this.History = Extensions.DeferExecutionOnce(WindowsLiveMessenger.GetHistory, this.ProfileID).AsSerializable();
				this.Accounts = Extensions.DeferExecutionOnce(WindowsLiveMessenger.GetAccounts, this.ProfileID).AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return this.ProfileID;
			}
		}

		[DataContract]
		public sealed class Account : IData
		{
			[DataMember]
			public Uri FormSubmitUrl { get; set; }
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public string Username { get; set; }

			public override string ToString()
			{
				return this.Username;
			}
		}

		[DataContract]
		public sealed class History : IData
		{
			[DataMember]
			public DateTime DateTime { get; set; }
			[DataMember]
			public string From { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(Converter = "MessageConverter")]
			public string Message { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(Ignore = true)]
			public string Style { get; set; }
			[DataMember]
			public string To { get; set; }

			public override string ToString()
			{
				return string.Format("{0} \x2192 {1}: {2}", this.From, this.To, XAML.TextWordAfterConverter.Instance.Convert(this.Message, 25));
			}

			private Inline MessageConverter(string Message)
			{
				Run FormattedMessage = new Run(Message);

				if (this.Style == null)
					return FormattedMessage;

				try
				{
					BoneSoft.CSS.CSSParser Parser = new BoneSoft.CSS.CSSParser();
					Parser.ParseText(string.Format("p {{ {0} }}", this.Style));
					
					foreach (BoneSoft.CSS.Declaration Declaration in Parser.CSSDocument.RuleSets[0].Declarations)
					{
						try
						{
							string Value = string.Join(" ", Declaration.Expression.Terms.Select(Term => Term.Value).ToArray());

							switch (Declaration.Name.ToLower())
							{
								case "color":
									FormattedMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Value));
									break;
								case "background-color": // I don't think this is supported but it is included in case.
									FormattedMessage.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Value));
									break;
								case "font-family":
									FormattedMessage.FontFamily = new FontFamily(Value);
									break;
								case "font-size":
									FontSizeConverter FontSizeConverter = new FontSizeConverter();
									FormattedMessage.FontSize = (double)FontSizeConverter.ConvertFromString(Value);
									break;
								case "font-style":
									FontStyleConverter FontStyleConverter = new FontStyleConverter();
									FormattedMessage.FontStyle = (FontStyle)FontStyleConverter.ConvertFromString(Value);
									break;
								case "font-weight":
									FontWeightConverter FontWeightConverter = new FontWeightConverter();
									FormattedMessage.FontWeight = (FontWeight)FontWeightConverter.ConvertFromString(Value);
									break;
							}
						}
						catch (Exception ex)
						{
							Utilities.Utilities.Log(ex);
						}
					}
				}
				catch (Exception ex)
				{
					Utilities.Utilities.Log(ex);
				}

				return FormattedMessage;
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Account> GetAccounts(string ProfileID)
		{
			List<Account> rData = new List<Account>();
			string UserName = "*";

			try
			{
				using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(WindowsLiveMessenger.MSNRegKey, false).OpenSubKey(ProfileID, false))
				{
					if (rk != null)
					{
						try
						{
							string UTL = rk.GetValue("UTL") as string;

							if (UTL != null)
							{
								XmlDocument Doc = new XmlDocument();
								Doc.XmlResolver = null;
								Doc.LoadXml(UTL);
								XmlNode msnobj = Doc.SelectSingleNode("/msnobj");
								XmlNode Creator = (msnobj != null) ? msnobj.Attributes.GetNamedItem("Creator") : null;

								if (Creator != null)
									UserName = Creator.Value;

								Doc = null;
							}
						}
						catch (Exception e)
						{
							Utilities.Utilities.Log(e);
						}
					}
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			IntPtr Credentials = IntPtr.Zero;

			try
			{
				uint Count;

				if (Utilities.Win32.CredEnumerate(string.Format("WindowsLive:name={0}", UserName), Utilities.Win32.CredEnumerateFlags.None, out Count, out Credentials))
				{
					for (int n = 0; n < Count; n++)
					{
						Utilities.Win32.CREDENTIAL Credential = default(Utilities.Win32.CREDENTIAL);

						try
						{
							IntPtr pCredential = Marshal.ReadIntPtr(Credentials, IntPtr.Size * n);
							Credential = (Utilities.Win32.CREDENTIAL)Marshal.PtrToStructure(pCredential, typeof(Utilities.Win32.CREDENTIAL));

							if ((UserName != "*" && Credential.UserName != UserName)
								|| Credential.CredentialBlob == IntPtr.Zero
								|| Credential.CredentialBlobSize == 0)
								continue;

							Uri TempUri;
							rData.Add(new Account
							{
								FormSubmitUrl = Uri.TryCreate(Credential.TargetName, UriKind.Absolute, out TempUri) ? TempUri : null,
								Password = Marshal.PtrToStringUni(Credential.CredentialBlob, (int)(Credential.CredentialBlobSize / 2)),
								Username = Credential.UserName
							});
						}
						catch (Exception e)
						{
							Utilities.Utilities.Log(e);
						}
					}
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			finally
			{
				if (Credentials != IntPtr.Zero)
					Utilities.Win32.CredFree(Credentials);
			}

			return rData;
		}

		private static IEnumerable<History> GetHistory(string ProfileID)
		{
			List<History> rData = new List<History>();
			
			try
			{
				using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(WindowsLiveMessenger.MSNRegKey, false).OpenSubKey(ProfileID, false))
				{
					if (rk == null)
						return Enumerable.Empty<History>();

					string MessageLogPath = rk.GetValue("MessageLogPath") as string;

					if (MessageLogPath == null)
						return Enumerable.Empty<History>();

					DirectoryInfo HistoryDir = new DirectoryInfo(MessageLogPath);

					if (!HistoryDir.Exists)
						return Enumerable.Empty<History>();

					foreach (FileInfo HistoryFile in HistoryDir.GetFiles("*.xml"))
					{
						string TempFile = Path.GetTempFileName();

						try
						{
							File.Copy(HistoryFile.FullName, TempFile, true);
							XmlDocument Doc = new XmlDocument();
							Doc.XmlResolver = null;
							Doc.Load(TempFile);

							foreach (XmlNode Message in Doc.SelectNodes("/Log/Message"))
							{
								XmlNode From = Message.SelectSingleNode("From/User");

								if (From != null)
									From = From.Attributes.GetNamedItem("FriendlyName");

								XmlNode To = Message.SelectSingleNode("To/User");

								if (To != null)
									To = To.Attributes.GetNamedItem("FriendlyName");

								XmlNode Text = Message.SelectSingleNode("Text");
								XmlNode TextStyle = (Text != null) ? Text.Attributes.GetNamedItem("Style") : null;
								XmlNode Time = Message.Attributes.GetNamedItem("DateTime");

								DateTime TempDateTime;
								rData.Add(new History
								{
									DateTime = (Time != null && DateTime.TryParse(Time.Value, out TempDateTime))
										? TempDateTime
										: default(DateTime),
									From = (From != null) ? From.Value : null,
									Message = (Text != null) ? Text.InnerText : null,
									Style = (TextStyle != null) ? TextStyle.Value : null,
									To = (To != null) ? To.Value : null
								});
							}

							Doc = null;
						}
						catch (Exception e)
						{
							Utilities.Utilities.Log(e);
						}
						finally
						{
							File.Delete(TempFile);
						}
					}
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			rData.Sort((a, b) =>
			{
				if (a.DateTime == null)
					return (b.DateTime == null) ? 0 : -1;

				return a.DateTime.CompareTo(b.DateTime);
			});
			return rData;
		}
		#endregion

		private const string MSNRegKey = @"SOFTWARE\Microsoft\MSNMessenger\PerPassportSettings";

		/**
		 * // SOFTWARE\Microsoft\MSNMessenger\PerPassportSettings
		 * 
		 * bool HistoryEnabled = false;
		 * 
		 * try
		 * {
		 *  HistoryEnabled = (int)sk.GetValue("MessageLoggingEnabled") != 0;
		 * }
		 * catch (InvalidCastException) //MessageLoggingEnabled is a byte[] when enabled and int 0 otherwise
		 * {
		 *  HistoryEnabled = true;
		 * }							 
		 */
	}
}