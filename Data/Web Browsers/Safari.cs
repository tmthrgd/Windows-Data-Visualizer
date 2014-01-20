using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class Safari : IData<Safari>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public Safari Initiate()
		{
			DirectoryInfo ProfilePath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Apple Computer", "Safari"));

			if (ProfilePath.Exists)
				this.Profiles = EnumerableEx.Return(new Profile(ProfilePath).Initiate()).AsSerializable();
			else
				this.Profiles = Enumerable.Empty<Profile>();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Safari"
				: string.Format("Safari ({0})", this.Profiles.Count());
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

			public Profile(DirectoryInfo Path)
			{
				this.ProfilePath = Path;
			}

			[DataMember]
			[Utilities.DisplayConfiguration("Path")]
			public DirectoryInfo ProfilePath { get; private set; }

			[DataMember]
			public IEnumerable<Bookmark> Bookmarks { get; private set; }
			[DataMember]
			public IEnumerable<Cookie> Cookies { get; private set; }
			[DataMember]
			public IEnumerable<History> History { get; private set; }
			[DataMember]
			public IEnumerable<StoredPassword> Passwords { get; private set; }
			[DataMember]
			public IEnumerable<TopSite> TopSites { get; private set; }

			public Profile Initiate()
			{
				this.Bookmarks = Extensions.DeferExecutionOnce(Safari.GetBookmarks, this.ProfilePath).AsSerializable();
				this.Cookies = Extensions.DeferExecutionOnce(Safari.GetCookies, this.ProfilePath).AsSerializable();
				this.History = Extensions.DeferExecutionOnce(Safari.GetHistory, this.ProfilePath).AsSerializable();
				this.Passwords = Extensions.DeferExecutionOnce(Safari.GetPasswords, this.ProfilePath).AsSerializable();
				this.TopSites = Extensions.DeferExecutionOnce(Safari.GetTopSites, this.ProfilePath).AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return this.ProfilePath.FullName;
			}
		}

		[DataContract]
		public sealed class Bookmark : IData
		{
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			public IEnumerable<Bookmark> Children { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }

			public override string ToString()
			{
				return (this.Url == null && this.Children != null)
					? string.Format("{0} ({1})", this.Title, this.Children.Count())
					: string.IsNullOrEmpty(this.Title)
						? this.Url.ToString()
						: string.Format("{0} ({1})", this.Title, this.Url);
			}
		}

		[DataContract]
		public sealed class Cookie : IData
		{
			[DataMember]
			public DateTime Created { get; set; }
			[DataMember]
			public DateTime Expiry { get; set; }
			[DataMember]
			public string Host { get; set; }
			[DataMember]
			public bool IsHttpOnly { get; set; }
			[DataMember]
			public bool IsSecure { get; set; }
			[DataMember]
			public string Path { get; set; }
			[DataMember]
			public string Name { get; set; }
			[DataMember]
			public string Value { get; set; }

			public override string ToString()
			{
				return string.Format("{0} ({1})", this.Name, this.Host);
			}
		}

		[DataContract]
		public sealed class History : IData
		{
			[DataMember]
			public DateTime LastVisit { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }
			[DataMember]
			public long Visits { get; set; }

			public override string ToString()
			{
				return (string.IsNullOrEmpty(this.Title) && this.Url == null)
					? base.ToString()
					: string.IsNullOrEmpty(this.Title)
						? this.Url.ToString()
						: (this.Url == null)
							? this.Title
							: string.Format("{0} ({1})", this.Title, this.Url);
			}
		}

		[DataContract]
		public sealed class StoredPassword : IData
		{
			[DataMember]
			public string Hostname { get; set; }
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri FormSubmitUrl { get; set; }
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public string Username { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Hostname)
					? this.Username
					: string.Format("{0} ({1})", this.Username, this.Hostname);
			}
		}

		[DataContract]
		public sealed class TopSite : IData
		{
			[DataMember]
			public bool BuiltIn { get; set; }
			[DataMember]
			public Uri FeedUrl { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Title)
					? this.Url.ToString()
					: string.Format("{0} ({1})", this.Title, this.Url);
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Bookmark> GetBookmarks(DirectoryInfo ProfilePath)
		{
			string BookmarksPath = Path.Combine(ProfilePath.FullName, "Bookmarks.plist");

			if (!File.Exists(BookmarksPath))
				return Enumerable.Empty<Bookmark>();

			string TempPath = Path.GetTempFileName();

			if (!Safari.Plist2Xml(BookmarksPath, TempPath))
			{
				File.Delete(TempPath);
				return Enumerable.Empty<Bookmark>();
			}

			XmlDocument Doc = new XmlDocument();

			try
			{

				Doc.XmlResolver = null;
				Doc.Load(TempPath);
				return Safari.ParseBookmarkTree(Safari.ParseDictionary(Doc.SelectSingleNode("/plist/dict")));
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<Bookmark>();
			}
			finally
			{
				Doc = null;
				File.Delete(TempPath);
			}
		}

		private static IEnumerable<Cookie> GetCookies(DirectoryInfo ProfilePath)
		{
			string CookiesPath = Path.Combine(ProfilePath.FullName, "Cookies", "Cookies.plist");

			if (!File.Exists(CookiesPath))
				return Enumerable.Empty<Cookie>();

			string TempPath = Path.GetTempFileName();

			if (!Safari.Plist2Xml(CookiesPath, TempPath))
			{
				File.Delete(TempPath);
				return Enumerable.Empty<Cookie>();
			}

			List<Cookie> rData = new List<Cookie>();

			try
			{
				XmlDocument Doc = new XmlDocument();
				Doc.XmlResolver = null;
				Doc.Load(TempPath);

				foreach (XmlElement Dict in Doc.SelectNodes("/plist/array/dict"))
				{
					IDictionary<string, XmlNode> Values = Safari.ParseDictionary(Dict);
					DateTime TempDateTime;
					bool TempBool;
					double TempDouble;
					rData.Add(new Cookie
					{
						Created = (Values.ContainsKey("Created")
							&& double.TryParse(Values["Created"].InnerText, out TempDouble))
								? Safari.CFAbsoluteTime.AddSeconds(TempDouble)
								: default(DateTime),
						Expiry = (Values.ContainsKey("Expires")
							&& Values["Expires"].LocalName == "date"
							&& DateTime.TryParse(Values["Expires"].InnerText, out TempDateTime))
								? TempDateTime
								: default(DateTime),
						Host = (Values.ContainsKey("Domain"))
							? Values["Domain"].InnerText
							: null,
						IsHttpOnly = (Values.ContainsKey("HttpOnly")
							&& bool.TryParse(Values["HttpOnly"].InnerText, out TempBool)) ? TempBool : false,
						IsSecure = (Values.ContainsKey("Secure")
							&& bool.TryParse(Values["Secure"].InnerText, out TempBool)) ? TempBool : false,
						Name = Values.ContainsKey("Name") ? Values["Name"].InnerText : null,
						Path = Values.ContainsKey("Path") ? Values["Path"].InnerText : null,
						Value = Values.ContainsKey("Value") ? Values["Value"].InnerText : null
					});
					Values.Clear();
				}

				Doc = null;
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			finally
			{
				File.Delete(TempPath);
			}

			return rData;
		}

		private static IEnumerable<History> GetHistory(DirectoryInfo ProfilePath)
		{
			string HistoryPath = Path.Combine(ProfilePath.FullName, "History.plist");

			if (!File.Exists(HistoryPath))
				return Enumerable.Empty<History>();

			string TempPath = Path.GetTempFileName();

			if (!Safari.Plist2Xml(HistoryPath, TempPath))
			{
				File.Delete(TempPath);
				return Enumerable.Empty<History>();
			}

			List<History> rData = new List<History>();

			try
			{
				XmlDocument Doc = new XmlDocument();
				Doc.XmlResolver = null;
				Doc.Load(TempPath);
				IDictionary<string, XmlNode> ParentDict = Safari.ParseDictionary(Doc.SelectSingleNode("/plist/dict"));

				if (!ParentDict.ContainsKey("WebHistoryDates"))
					return new History[0];

				foreach (XmlNode Dict in ParentDict["WebHistoryDates"])
				{
					IDictionary<string, XmlNode> Values = Safari.ParseDictionary(Dict);

					if (!Values.ContainsKey(string.Empty))
						continue;

					Uri TempUri;
					int TempInt;
					double TempDouble;
					rData.Add(new History
					{
						LastVisit = (Values.ContainsKey("lastVisitedDate")
							&& double.TryParse(Values["lastVisitedDate"].InnerText, out TempDouble))
							? Safari.CFAbsoluteTime.AddSeconds(TempDouble)
							: default(DateTime),
						Title = Values.ContainsKey("title") ? Values["title"].InnerText : null,
						Url = Uri.TryCreate(Values[string.Empty].InnerText, UriKind.Absolute, out TempUri) ? TempUri : null,
						Visits = (Values.ContainsKey("visitCount") && int.TryParse(Values["visitCount"].InnerText, out TempInt)) ? TempInt : -1
					});
					Values.Clear();
				}

				ParentDict.Clear();
				Doc = null;
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			finally
			{
				File.Delete(TempPath);
			}

			return rData;
		}

		private static IEnumerable<StoredPassword> GetPasswords(DirectoryInfo ProfilePath)
		{
			string PasswordsPath = Path.Combine(ProfilePath.Parent.FullName, "Preferences", "keychain.plist");

			if (!File.Exists(PasswordsPath))
				return Enumerable.Empty<StoredPassword>();

			string TempPath = Path.GetTempFileName();

			if (!Safari.Plist2Xml(PasswordsPath, TempPath))
			{
				File.Delete(TempPath);
				return Enumerable.Empty<StoredPassword>();
			}

			List<StoredPassword> rData = new List<StoredPassword>();
			byte[] OptionalEntropy = Convert.FromBase64String("Hayo+NO4SD5IfT4KYgfdJuZngQPnshOlsHnuTw9BFe17FIzlS0YNwY7+1ucndQaLSQDcDzCgnv0JhfHIqnXBCAV5AeKX2K+AOGALcQ5oU3cvD2H2HY6PXLI9IXRAS7UGbqt6vYupfjKPbgYk2Smkpb4mI/3u8UwPdF5Y+5F075Fjb20uYXBwbGUuU2FmYXJp");
			
			try
			{
				XmlDocument Doc = new XmlDocument();
				Doc.XmlResolver = null;
				Doc.Load(TempPath);
				IDictionary<string, XmlNode> VersionDict = Safari.ParseDictionary(Doc.SelectSingleNode("/plist/dict"));

				if (!VersionDict.ContainsKey("version1"))
					return Enumerable.Empty<StoredPassword>();

				foreach (XmlNode Dict in VersionDict["version1"].ChildNodes)
				{
					IDictionary<string, XmlNode> Values = Safari.ParseDictionary(Dict);
					StoredPassword Password = new StoredPassword();

					if (Values.ContainsKey("Data"))
					{
						string Base64Password = Values["Data"].InnerText
							.Replace("\r\n", string.Empty)
							.Replace("\n", string.Empty)
							.Replace("\t", string.Empty);

						try
						{
							byte[] EncryptedPassword = Convert.FromBase64String(Base64Password);
							byte[] PlainTextBytes = ProtectedData.Unprotect(EncryptedPassword, OptionalEntropy, DataProtectionScope.CurrentUser);
							int PasswordLength = BitConverter.ToInt32(PlainTextBytes, 0);
							Password.Password = Encoding.UTF8.GetString(PlainTextBytes, sizeof(int), PasswordLength);
							Array.Clear(PlainTextBytes, 0, PlainTextBytes.Length);
						}
						catch (Exception e)
						{
							Utilities.Utilities.Log(e);
						}
					}

					if (Values.ContainsKey("Account"))
						Password.Username = Values["Account"].InnerText;

					UriBuilder FormSubmitUrl = new UriBuilder();

					if (Values.ContainsKey("Protocol"))
						// Not sure about this, Values["Protocol"] == "1752462448" == ptth
						FormSubmitUrl.Scheme = (Values["Protocol"].InnerText == "1752462448")
							? Uri.UriSchemeHttps
							: Uri.UriSchemeHttp;

					if (Values.ContainsKey("Path"))
						FormSubmitUrl.Path = Values["Path"].InnerText;

					int TempInt;

					if (Values.ContainsKey("Port")
						&& int.TryParse(Values["Port"].InnerText, out TempInt)
						&& TempInt != 0)
						FormSubmitUrl.Port = TempInt;

					if (Values.ContainsKey("Server"))
						Password.Hostname
							= FormSubmitUrl.Host
							= Values["Server"].InnerText;

					try
					{
						Password.FormSubmitUrl = FormSubmitUrl.Uri;
					}
					catch (Exception e)
					{
						Utilities.Utilities.Log(e);
					}

					// Values["AuthenticationType"] == "1836216166" == form
					rData.Add(Password);
				}

				VersionDict.Clear();
				Doc = null;
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			finally
			{
				File.Delete(TempPath);
			}

			Array.Clear(OptionalEntropy, 0, OptionalEntropy.Length);
			return rData;
		}

		private static IEnumerable<TopSite> GetTopSites(DirectoryInfo ProfilePath)
		{
			string TopSitesPath = Path.Combine(ProfilePath.FullName, "TopSites.plist");

			if (!File.Exists(TopSitesPath))
				return Enumerable.Empty<TopSite>();

			string TempPath = Path.GetTempFileName();

			if (!Safari.Plist2Xml(TopSitesPath, TempPath))
			{
				File.Delete(TempPath);
				return Enumerable.Empty<TopSite>();
			}

			List<TopSite> rData = new List<TopSite>();

			try
			{
				XmlDocument Doc = new XmlDocument();
				Doc.XmlResolver = null;
				Doc.Load(TempPath);
				IDictionary<string, XmlNode> ParentDict = Safari.ParseDictionary(Doc.SelectSingleNode("/plist/dict"));

				if (!ParentDict.ContainsKey("TopSites"))
					return new TopSite[0];

				foreach (XmlNode Dict in ParentDict["TopSites"])
				{
					IDictionary<string, XmlNode> Values = Safari.ParseDictionary(Dict);
					Uri TempUri;
					rData.Add(new TopSite
					{
						BuiltIn = (Values.ContainsKey("TopSiteIsBuiltIn")
							&& Values["TopSiteIsBuiltIn"].Name == "true"),
						FeedUrl = (Values.ContainsKey("TopSiteFeedURLString")
							&& Uri.TryCreate(Values["TopSiteFeedURLString"].InnerText, UriKind.Absolute, out TempUri)) ? TempUri : null,
						Title = Values.ContainsKey("TopSiteTitle")
							? Values["TopSiteTitle"].InnerText
							: null,
						Url = (Values.ContainsKey("TopSiteURLString")
							&& Uri.TryCreate(Values["TopSiteURLString"].InnerText, UriKind.Absolute, out TempUri)) ? TempUri : null
					});
				}

				ParentDict.Clear();
				Doc = null;
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			finally
			{
				File.Delete(TempPath);
			}

			return rData;
		}

		private static IEnumerable<Bookmark> ParseBookmarkTree(IDictionary<string, XmlNode> List)
		{
			if (!List.ContainsKey("Children"))
				return Enumerable.Empty<Bookmark>();

			List<Bookmark> rData = new List<Bookmark>();

			foreach (XmlNode Dict in List["Children"].ChildNodes)
			{
				IDictionary<string, XmlNode> Values = Safari.ParseDictionary(Dict);

				if (!Values.ContainsKey("WebBookmarkType"))
					continue;

				switch (Values["WebBookmarkType"].InnerText)
				{
					case "WebBookmarkTypeList":
						rData.Add(new Bookmark
						{
							Children = Safari.ParseBookmarkTree(Values).AsSerializable(),
							Title = Values.ContainsKey("Title") ? Values["Title"].InnerText : null
						});
						break;
					case "WebBookmarkTypeLeaf":
						IDictionary<string, XmlNode> LeafValues = Values.ContainsKey("URIDictionary") ? Safari.ParseDictionary(Values["URIDictionary"]) : new Dictionary<string, XmlNode>();
						Uri TempUri;
						rData.Add(new Bookmark
						{
							Title = LeafValues.ContainsKey("title") ? LeafValues["title"].InnerText : null,
							Url = (Values.ContainsKey("URLString") && Uri.TryCreate(Values["URLString"].InnerText, UriKind.Absolute, out TempUri)) ? TempUri : null
						});
						LeafValues.Clear();
						break;
				}

				Values.Clear();
			}

			return rData;
		}

		private static IDictionary<string, XmlNode> ParseDictionary(XmlNode Dict)
		{
			if (Dict.LocalName != "dict")
				throw new ArgumentException("Paramater not a <dict>.", "Dict");

			IDictionary<string, XmlNode> Values = new Dictionary<string, XmlNode>(Dict.ChildNodes.Count / 2);

			for (int i = 0; i < Dict.ChildNodes.Count; i += 2)
			{
				XmlNode Key = Dict.ChildNodes.Item(i + 0);
				XmlNode Value = Dict.ChildNodes.Item(i + 1);

				if (Key != null
					&& Value != null
					&& Key.LocalName == "key"
					&& !Values.ContainsKey(Key.InnerText))
					Values.Add(Key.InnerText, Value);
			}

			return Values;
		}

		private static bool Plist2Xml(string FileName, string ResultFile)
		{
			try
			{
				using (FileStream FS = File.OpenRead(FileName))
				using (StreamReader SR = new StreamReader(FS))
				{
					char[] Header = new char[8];
					SR.Read(Header, 0, Header.Length);

					switch (new string(Header))
					{
						case "bplist00":
							return Safari.BinaryPlist2XML(FileName, ResultFile);
						case "<?xml ve":
							File.Copy(FileName, ResultFile, true);
							return true;
						default:
							return false;
					}
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return false;
			}
		}

		private static bool BinaryPlist2XML(string FileName, string ResultFile)
		{
			if (Safari.AppleAplicationSupportLocation == null)
			{
				try
				{
					using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(AppleAplicationSupportKey, false))
						if (rk != null)
							Safari.AppleAplicationSupportLocation = rk.GetValue("InstallDir") as string;
				}
				catch (Exception e)
				{
					Utilities.Utilities.Log(e);
				}

				if (IntPtr.Size == 8 && (Safari.AppleAplicationSupportLocation == null || !Directory.Exists(Safari.AppleAplicationSupportLocation)))
				{
					try
					{
						using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(AppleAplicationSupportKey32, false))
							if (rk != null)
								Safari.AppleAplicationSupportLocation = rk.GetValue("InstallDir") as string;
					}
					catch (Exception e)
					{
						Utilities.Utilities.Log(e);
					}
				}

				if (Safari.AppleAplicationSupportLocation == null || !Directory.Exists(Safari.AppleAplicationSupportLocation))
					Safari.AppleAplicationSupportLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "Apple", "Apple Application Support");
			}

			string PlutilPath = Path.Combine(Safari.AppleAplicationSupportLocation, "plutil.exe");

			if (!File.Exists(PlutilPath))
				return false;

			using (Process Plutil = new Process())
			{
				Plutil.StartInfo = new ProcessStartInfo(PlutilPath, string.Format("-convert xml1 -o \"{0}\" \"{1}\"", ResultFile, FileName));
				Plutil.StartInfo.CreateNoWindow = true;
				Plutil.StartInfo.UseShellExecute = false;
				return Plutil.Start()
					&& Plutil.WaitForExit(1200000) // wait 2 minutes or fail
					&& File.Exists(ResultFile);
			}
		}
		#endregion

		private static string AppleAplicationSupportLocation;
		private static DateTime CFAbsoluteTime = new DateTime(2001, 1, 1);

		private const string AppleAplicationSupportKey = @"SOFTWARE\Apple Inc.\Apple Application Support";
		private const string AppleAplicationSupportKey32 = @"SOFTWARE\Wow6432Node\Apple Inc.\Apple Application Support";
	}
}