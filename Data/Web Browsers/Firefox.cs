using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class Firefox : IData<Firefox>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public Firefox Initiate()
		{
			string FirefoxAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox");
			string ProfilesIniPath = Path.Combine(FirefoxAppData, "profiles.ini");

			if (File.Exists(ProfilesIniPath))
			{
				Utilities.IniParser ProfilesIni = new Utilities.IniParser(ProfilesIniPath);
				List<Profile> Profiles = new List<Profile>();
				int temp;

				foreach (KeyValuePair<string, IDictionary<string, string>> profile in ProfilesIni)
				{
					if (!profile.Key.StartsWith("Profile", true, null) || !profile.Value.ContainsKey("Path"))
						continue;

					if (int.TryParse(profile.Value["IsRelative"], out temp) && temp == 1)
						Profiles.Add(new Profile(new DirectoryInfo(Path.Combine(FirefoxAppData, profile.Value["Path"]))).Initiate());
					else
						Profiles.Add(new Profile(new DirectoryInfo(profile.Value["Path"])).Initiate());
				}

				this.Profiles = Profiles.Memoize().AsSerializable();
			}
			else
			{
				DirectoryInfo FirefoxProfilesDir = new DirectoryInfo(Path.Combine(FirefoxAppData, "Profiles"));

				if (FirefoxProfilesDir.Exists)
					this.Profiles = FirefoxProfilesDir.GetDirectories().Select(dir => new Profile(dir).Initiate()).Memoize().AsSerializable();
				else
					this.Profiles = Enumerable.Empty<Profile>();
			}

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Firefox"
				: string.Format("Firefox ({0})", this.Profiles.Count());
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
			public IEnumerable<Download> Downloads { get; private set; }
			[DataMember]
			public IEnumerable<FormHistory> FormHistory { get; private set; }
			[DataMember]
			public IEnumerable<History> History { get; private set; }
			[DataMember]
			public IEnumerable<StoredPassword> Passwords { get; private set; }
			[DataMember]
			public IEnumerable<Window> Windows { get; private set; }

			public Profile Initiate()
			{
				this.Bookmarks = Extensions.DeferExecutionOnce(Firefox.GetBookmarks, this.ProfilePath).AsSerializable();
				this.Cookies = Extensions.DeferExecutionOnce(Firefox.GetCookies, this.ProfilePath).AsSerializable();
				this.Downloads = Extensions.DeferExecutionOnce(Firefox.GetDownloads, this.ProfilePath).AsSerializable();
				this.FormHistory = Extensions.DeferExecutionOnce(Firefox.GetFormHistory, this.ProfilePath).AsSerializable();
				this.History = Extensions.DeferExecutionOnce(Firefox.GetHistory, this.ProfilePath).AsSerializable();
				this.Passwords = Extensions.DeferExecutionOnce(Firefox.GetPasswords, this.ProfilePath).AsSerializable();
				this.Windows = Extensions.DeferExecutionOnce(Firefox.GetWindows, this.ProfilePath).AsSerializable();

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
			public DateTime DateAdded { get; set; }
			[DataMember]
			public DateTime DateModified { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(Converter = "TagsToString", EmitDefaultValue = false)]
			public string[] Tags { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			public IEnumerable<Bookmark> Bookmarks { get; set; }

			public override string ToString()
			{
				return (this.Url == null && this.Bookmarks != null)
					? string.Format("{0} ({1})", this.Title, this.Bookmarks.Count())
					: string.IsNullOrEmpty(this.Title)
						? this.Url.ToString()
						: string.Format("{0} ({1})", this.Title, this.Url);
			}

			private string TagsToString(string[] Tags)
			{
				return string.Join(", ", Tags);
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
			public DateTime LastAccessed { get; set; }
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
		public sealed class Download : IData
		{
			[DataMember]
			public string MimeType { get; set; }
			[DataMember]
			public string Name { get; set; }
			[DataMember]
			public Uri Referrer { get; set; }
			[DataMember]
			public Uri SourceUrl { get; set; }
			[DataMember]
			public DateTime Started { get; set; }
			[DataMember]
			public Uri TargetUrl { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			public Uri TemporaryFile { get; set; }

			public override string ToString()
			{
				return (this.SourceUrl == null)
					? this.Name
					: string.Format("{0} ({1})", this.Name, this.SourceUrl);
			}
		}

		[DataContract]
		public sealed class FormHistory : IData
		{
			[DataMember]
			public DateTime FirstUsed { get; set; }
			[DataMember]
			public DateTime LastUsed { get; set; }
			[DataMember]
			public string Name { get; set; }
			[DataMember]
			public long TimesUsed { get; set; }
			[DataMember]
			public string Value { get; set; }

			public override string ToString()
			{
				return this.Name;
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
			public long Typed { get; set; }
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }
			[DataMember]
			public long Visits { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Title)
					? this.Url.ToString()
					: string.Format("{0} ({1})", this.Title, this.Url);
			}
		}

		[DataContract]
		public sealed class StoredPassword : IData
		{
			[DataMember]
			public DateTime Created { get; set; }
			[DataMember]
			public string Hostname { get; set; }
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri FormSubmitUrl { get; set; }
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public string PasswordField { get; set; }
			[DataMember]
			public string Username { get; set; }
			[DataMember]
			public string UsernameField { get; set; }
			[DataMember]
			public DateTime LastUsed { get; set; }
			[DataMember]
			public long TimesUsed { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Hostname)
					? this.Username
					: string.Format("{0} ({1})", this.Username, this.Hostname);
			}
		}

		[DataContract]
		public sealed class Window : IData
		{
			[DataMember]
			public IEnumerable<Tab> Children { get; set; }
			[DataMember]
			public string Title { get; set; }

			public override string ToString()
			{
				return string.Format("{0} ({1})", this.Title, this.Children.Count());
			}
		}

		[DataContract]
		public sealed class Tab : IData
		{
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			public IEnumerable<Window> Children { get; set; }
			[DataMember]
			public DateTime LastUpdated { get; set; }
			[DataMember]
			public bool Pinned { get; set; }
			[DataMember]
			public Uri Referer { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }
			[DataMember]
			public string UserTypedValue { get; set; }

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
			DirectoryInfo BookmarksDirectory = new DirectoryInfo(Path.Combine(ProfilePath.FullName, "bookmarkbackups"));

			if (!BookmarksDirectory.Exists)
				return Enumerable.Empty<Bookmark>();

			FileInfo NewestBookmarkFile = BookmarksDirectory.GetFiles("bookmarks-*-*-*.json")
				.OrderByDescending(BookmarkFile =>
				{
					try
					{
						string[] FileNameParts = BookmarkFile.Name.Split('-', '.');
						int Year, Month, Day;
						int.TryParse(FileNameParts[1], out Year);
						int.TryParse(FileNameParts[2], out Month);
						int.TryParse(FileNameParts[3], out Day);
						return new DateTime(Year, Month, Day);
					}
					catch
					{
						return DateTime.MinValue;
					}
				}).FirstOrDefault();

			if (NewestBookmarkFile == default(FileInfo))
				return Enumerable.Empty<Bookmark>();

			try
			{
				Helpers.Bookmark Bookmarks = Helpers.Bookmark.Create(NewestBookmarkFile.FullName);

				if (Bookmarks.Type != "text/x-moz-place-container")
					return Enumerable.Empty<Bookmark>();

				Dictionary<string, List<string>> Tags = new Dictionary<string, List<string>>();

				foreach (Helpers.Bookmark BookmarksMenu in Bookmarks.Children)
				{
					if (BookmarksMenu.Root != "tagsFolder" || BookmarksMenu.Type != "text/x-moz-place-container")
						continue;

					foreach (Helpers.Bookmark Tag in BookmarksMenu.Children)
					{
						if (Tag.Type != "text/x-moz-place-container")
							continue;

						foreach (Helpers.Bookmark Bookmark in Tag.Children)
						{
							if (Bookmark.Type != "text/x-moz-place")
								continue;

							if (!Tags.ContainsKey(Bookmark.Uri))
							{
								Tags.Add(Bookmark.Uri, new List<string>()
								{
									Tag.Title
								});
							}
							else
								Tags[Bookmark.Uri].Add(Tag.Title);
						}
					}
				}

				return Bookmarks.Children
					.Where(bookmark => bookmark.Root != "tagsFolder")
					.Select(bookmark => Firefox.ParseBookmark(bookmark, Tags))
					.Where(bookmark => bookmark != null);
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<Bookmark>();
			}
		}

		private static IEnumerable<Cookie> GetCookies(DirectoryInfo ProfilePath)
		{
			string CookiesPath = Path.Combine(ProfilePath.FullName, "cookies.sqlite");

			if (!File.Exists(CookiesPath))
				return Enumerable.Empty<Cookie>();

			List<Cookie> rData = new List<Cookie>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(CookiesPath))
				{
					Connection.ForEach("SELECT creationTime, expiry, host, isHttpOnly, lastAccessed, path, name, isSecure, value FROM moz_cookies", DataReader =>
					{
						//id, baseDomain, name, value, host, path, expiry, lastAccessed, isSecure, isHttpOnly, creationTime
						long TempLong;
						rData.Add(new Cookie
						{
							Created = long.TryParse(DataReader["creationTime"].ToString(), out TempLong)
								? Firefox.UnixEpoch.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							Expiry = long.TryParse(DataReader["expiry"].ToString(), out TempLong)
								? Firefox.UnixEpoch.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							Host = DataReader["host"].ToString(),
							IsHttpOnly = DataReader["isHttpOnly"].ToString() == "1",
							IsSecure = DataReader["isSecure"].ToString() == "1",
							LastAccessed = long.TryParse(DataReader["lastAccessed"].ToString(), out TempLong)
								? Firefox.UnixEpoch.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							Path = DataReader["path"].ToString(),
							Name = DataReader["name"].ToString(),
							Value = DataReader["value"].ToString()
						});
					});
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			return rData;
		}

		private static IEnumerable<Download> GetDownloads(DirectoryInfo ProfilePath)
		{
			string DownloadsPath = Path.Combine(ProfilePath.FullName, "downloads.sqlite");

			if (!File.Exists(DownloadsPath))
				return Enumerable.Empty<Download>();

			List<Download> rData = new List<Download>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(DownloadsPath))
				{
					Connection.ForEach("SELECT mimeType, name, source, target, referrer, tempPath FROM moz_downloads", DataReader =>
					{
						//id, name, source, target, tempPath, startTime, endTime, state, referrer, entityID, currBytes, maxBytes, mimeType, preferredAplication, preferedAction, autoResume
						Uri TempUri;
						rData.Add(new Download
						{
							MimeType = string.IsNullOrWhiteSpace(DataReader["mimeType"].ToString())
								? Utilities.Utilities.GetMimeType(Path.GetFileName(DataReader["target"].ToString()))
								: DataReader["mimeType"].ToString(),
							Name = DataReader["name"].ToString(),
							Referrer = Uri.TryCreate(DataReader["referrer"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null,
							SourceUrl = Uri.TryCreate(DataReader["source"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null,
							TargetUrl = Uri.TryCreate(DataReader["target"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null,
							TemporaryFile = Uri.TryCreate(DataReader["tempPath"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null
						});
					});
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			return rData;
		}

		private static IEnumerable<FormHistory> GetFormHistory(DirectoryInfo ProfilePath)
		{
			string FormHistoryPath = Path.Combine(ProfilePath.FullName, "formhistory.sqlite");

			if (!File.Exists(FormHistoryPath))
				return Enumerable.Empty<FormHistory>();

			List<FormHistory> rData = new List<FormHistory>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(FormHistoryPath))
				{
					Connection.ForEach("SELECT fieldname, value, timesUsed, firstUsed, lastUsed FROM moz_formhistory", DataReader =>
					{
						//id, fieldname, value, timesUsed, firstUsed, lastUsed, guid
						long TempLong;
						int TempInt;
						rData.Add(new FormHistory
						{
							Name = DataReader["fieldname"].ToString(),
							FirstUsed = long.TryParse(DataReader["firstUsed"].ToString(), out TempLong)
								? Firefox.UnixEpoch.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							LastUsed = long.TryParse(DataReader["lastUsed"].ToString(), out TempLong)
								? Firefox.UnixEpoch.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							TimesUsed = int.TryParse(DataReader["timesUsed"].ToString(), out TempInt) ? TempInt : 0,
							Value = DataReader["value"].ToString()
						});
					});
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			return rData;
		}

		private static IEnumerable<History> GetHistory(DirectoryInfo ProfilePath)
		{
			string HistoryPath = Path.Combine(ProfilePath.FullName, "places.sqlite");

			if (!File.Exists(HistoryPath))
				return Enumerable.Empty<History>();

			List<History> rData = new List<History>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(HistoryPath))
				{
					Connection.ForEach("SELECT last_visit_date, title, url, visit_count FROM moz_places", DataReader =>
					{
						//id, url, title, rev_host, visit_count, hidden, typed, favicon_id, frequency, last_visit_date
						long TempLong;
						int TempInt;
						Uri TempUri;
						rData.Add(new History
						{
							LastVisit = long.TryParse(DataReader["last_visit_date"].ToString(), out TempLong)
								? Firefox.UnixEpoch.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							Title = DataReader["title"].ToString(),
							Url = Uri.TryCreate(DataReader["url"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null,
							Visits = int.TryParse(DataReader["visit_count"].ToString(), out TempInt) ? TempInt : -1
						});
						TempUri = null;
					});
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			return rData;
		}

		private static IEnumerable<StoredPassword> GetPasswords(DirectoryInfo ProfilePath)
		{
			if (Firefox.NSSInitProfile != null && Firefox.NSSInitProfile != ProfilePath.FullName)
			{
				Utilities.Utilities.Log("NSS has already initialized and cannot be reinitialized.");
				return Enumerable.Empty<StoredPassword>();
			}

			string SignonsPath = Path.Combine(ProfilePath.FullName, "signons.sqlite");

			if (!File.Exists(SignonsPath))
				return Enumerable.Empty<StoredPassword>();

			List<StoredPassword> rData = new List<StoredPassword>();

			try
			{
				Firefox.LoadFirefoxDlls();

				if (!Firefox.LibrariesLoaded)
					return Enumerable.Empty<StoredPassword>();

				Firefox.NSSInitProfile = ProfilePath.FullName;
				Firefox.NSSInit(ProfilePath.FullName);
				Firefox.PK11Authenticate(Firefox.PK11GetInternalKeySlot(), true, 0);

				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(SignonsPath))
				{
					Connection.ForEach("SELECT encryptedUsername, encryptedPassword, formSubmitURL, hostname, passwordField, usernameField, timeCreated, timeLastUsed, timesUsed FROM moz_logins", DataReader =>
					{
						IntPtr hi2 = IntPtr.Zero;
						IntPtr hi22 = IntPtr.Zero;

						try
						{
							//id, hostname, httpRealm, formSubmitURL, usernameField, passwordField, encryptedUsername, encryptedPassword, guid, encType, timeCreated, timeLastUsed, timePasswordChanged, timesUsed
							string Username = null;
							string Password = null;

							StringBuilder se = new StringBuilder(DataReader["encryptedUsername"].ToString());
							hi2 = Firefox.NSSBase64DecodeBuffer(IntPtr.Zero, IntPtr.Zero, se, se.Length);
							TSECItem item = (TSECItem)Marshal.PtrToStructure(hi2, typeof(TSECItem));
							se.Clear();

							TSECItem tSecDec = new TSECItem();

							if (Firefox.PK11SDRDecrypt(ref item, ref tSecDec, 0) == 0 && tSecDec.SECItemLen != 0)
							{
								byte[] bvRet = new byte[tSecDec.SECItemLen];
								Marshal.Copy(tSecDec.SECItemData, bvRet, 0, tSecDec.SECItemLen);
								Username = Encoding.UTF8.GetString(bvRet);
								Array.Clear(bvRet, 0, bvRet.Length);
								bvRet = null;
							}

							se = new StringBuilder(DataReader["encryptedPassword"].ToString());
							hi22 = Firefox.NSSBase64DecodeBuffer(IntPtr.Zero, IntPtr.Zero, se, se.Length);
							item = (TSECItem)Marshal.PtrToStructure(hi22, typeof(TSECItem));
							se.Clear();

							tSecDec = new TSECItem();

							if (Firefox.PK11SDRDecrypt(ref item, ref tSecDec, 0) == 0 && tSecDec.SECItemLen != 0)
							{
								byte[] bvRet = new byte[tSecDec.SECItemLen];
								Marshal.Copy(tSecDec.SECItemData, bvRet, 0, tSecDec.SECItemLen);
								Password = Encoding.UTF8.GetString(bvRet);
								Array.Clear(bvRet, 0, bvRet.Length);
								bvRet = null;
							}

							if (!string.IsNullOrWhiteSpace(Username))
							{
								long TempLong;
								Uri TempUri;
								rData.Add(new StoredPassword
								{
									Created = long.TryParse(DataReader["timeCreated"].ToString(), out TempLong)
										? Firefox.UnixEpoch.AddMilliseconds(TempLong / 1000)
										: default(DateTime),
									LastUsed = long.TryParse(DataReader["timeLastUsed"].ToString(), out TempLong)
										? Firefox.UnixEpoch.AddMilliseconds(TempLong / 1000)
										: default(DateTime),
									FormSubmitUrl = Uri.TryCreate(DataReader["formSubmitURL"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null,
									Hostname = Uri.TryCreate(DataReader["hostname"].ToString(), UriKind.Absolute, out TempUri) ? TempUri.Host : null,
									Password = Password,
									PasswordField = DataReader["passwordField"].ToString(),
									TimesUsed = long.TryParse(DataReader["timesUsed"].ToString(), out TempLong) ? TempLong : 0,
									Username = Username,
									UsernameField = DataReader["usernameField"].ToString()
								});
							}
						}
						finally
						{
							if (hi2 != IntPtr.Zero)
								Marshal.DestroyStructure(hi2, typeof(TSECItem));

							if (hi22 != IntPtr.Zero)
								Marshal.DestroyStructure(hi22, typeof(TSECItem));
						}
					});
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			finally
			{
				if (Firefox.NSSShutdown != null)
					Firefox.NSSShutdown();

				Firefox.FreeFirefoxDlls();
			}

			return rData;
		}

		private static IEnumerable<Window> GetWindows(DirectoryInfo ProfilePath)
		{
			string SessionPath = Path.Combine(ProfilePath.FullName, "sessionstore.js");

			if (!File.Exists(SessionPath))
				// Possibly SeaMonkey only, or old Firefox
				SessionPath = Path.Combine(ProfilePath.FullName, "sessionstore.json");

			if (!File.Exists(SessionPath))
				return Enumerable.Empty<Window>();

			try
			{
				Helpers.Session Session = Helpers.Session.Create(SessionPath);
				return Firefox.ParseSession(Session);
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<Window>();
			}
		}

		private static Bookmark ParseBookmark(Helpers.Bookmark Bookmark, Dictionary<string, List<string>> Tags)
		{
			switch (Bookmark.Type)
			{
				case "text/x-moz-place-container":
					return new Bookmark
					{
						Bookmarks = Bookmark.Children
							.Select(bookmark => Firefox.ParseBookmark(bookmark, Tags))
							.Where(bookmark => bookmark != null)
							.Memoize()
							.AsSerializable(),
						DateAdded = Firefox.UnixEpoch.AddMilliseconds(Bookmark.DateAdded / 1000),
						DateModified = Firefox.UnixEpoch.AddMilliseconds(Bookmark.LastModified / 1000),
						Title = Bookmark.Title,
					};
				case "text/x-moz-place":
					Uri TempUri;
					return new Bookmark
					{
						DateAdded = Firefox.UnixEpoch.AddMilliseconds(Bookmark.DateAdded / 1000),
						DateModified = Firefox.UnixEpoch.AddMilliseconds(Bookmark.LastModified / 1000),
						Tags = Tags.ContainsKey(Bookmark.Uri)
							? Tags[Bookmark.Uri].ToArray()
							: null,
						Title = Bookmark.Title,
						Url = Uri.TryCreate(Bookmark.Uri, UriKind.Absolute, out TempUri) ? TempUri : null
					};
				case "text/x-moz-place-separator":
					return null;
				default:
					Utilities.Utilities.Log("Firefox.ParseBookmark Unkown Bookmark Type: {0}", Bookmark.Type);
					return null;
			}
		}

		private static IEnumerable<Window> ParseSession(Helpers.Session Session)
		{
			List<Window> rData = new List<Window>(Session.Windows.Length);

			foreach (Helpers.SessionWindow Window in Session.Windows)
			{
				List<Tab> Tabs = new List<Tab>(Window.Tabs.Length);

				foreach (Helpers.SessionTab Tab in Window.Tabs)
				{
					if (Tab.Index > 0)
					{
						Helpers.SessionTabEntry TabEntry = Tab.Entries[Tab.Index - 1];

						Uri TempUri;
						Tabs.Add(new Tab
						{
							Children = (TabEntry.URL == "about:sessionrestore" && TabEntry.FormData != null)
								? (TabEntry.FormData.ID != null && TabEntry.FormData.ID.SessionData != null)
									? Firefox.ParseSession(TabEntry.FormData.ID.SessionData).Memoize().AsSerializable()
									: (TabEntry.FormData.SessionData != null)
										? Firefox.ParseSession(TabEntry.FormData.SessionData).Memoize().AsSerializable()
										: null
								: null,
							Pinned = Tab.Pinned,
							Referer = Uri.TryCreate(TabEntry.Referrer, UriKind.Absolute, out TempUri) ? TempUri : null,
							Title = TabEntry.Title,
							Url = Uri.TryCreate(TabEntry.URL, UriKind.Absolute, out TempUri) ? TempUri : null,
							UserTypedValue = Tab.UserTypedValue
						});
						
						if (TabEntry.URL == "about:sessionrestore" && Tabs.Last().Children == null)
							Utilities.Utilities.Log("Firefox.ParseSession(): Tab url is about:sessionrestore but contains no child tabs.");
					}
				}

				rData.Add(new Window
				{
					Children = Tabs.AsSerializable(),
					Title = Window.Title ?? "Window"
				});
			}

			return rData;
		}
		#endregion

		#region PasswordDependancies
		private struct TSECItem
		{
			public int SECItemType;
			public IntPtr SECItemData;
			public int SECItemLen;
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate long NSSInitPrototype(string configdir);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate long PK11GetInternalKeySlotPrototype();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate long PK11AuthenticatePrototype(long slot, bool loadCerts, long wincx);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr NSSBase64DecodeBufferPrototype(IntPtr arenaOpt, IntPtr outItemOpt, StringBuilder inStr, int inLen);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int PK11SDRDecryptPrototype(ref TSECItem data, ref TSECItem result, int cx);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int NSSShutdownPrototype();

		private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
		private const string UninstallKey32 = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

		private static IntPtr mozglue = IntPtr.Zero;
		private static IntPtr mozcrt19 = IntPtr.Zero;
		private static IntPtr nspr4 = IntPtr.Zero;
		private static IntPtr plc4 = IntPtr.Zero;
		private static IntPtr plds4 = IntPtr.Zero;
		private static IntPtr ssutil3 = IntPtr.Zero;
		private static IntPtr sqlite3 = IntPtr.Zero;
		private static IntPtr mozsqlite3 = IntPtr.Zero;
		private static IntPtr nssutil3 = IntPtr.Zero;
		private static IntPtr softokn3 = IntPtr.Zero;
		private static IntPtr NSS3 = IntPtr.Zero;
		private static NSSInitPrototype NSSInit;
		private static PK11GetInternalKeySlotPrototype PK11GetInternalKeySlot;
		private static PK11AuthenticatePrototype PK11Authenticate;
		private static NSSBase64DecodeBufferPrototype NSSBase64DecodeBuffer;
		private static PK11SDRDecryptPrototype PK11SDRDecrypt;
		private static NSSShutdownPrototype NSSShutdown;

		private static string NSSInitProfile = null;
		private static object LibrariesLoadedLock = new object();
		private static bool LibrariesLoaded = false;
		private static int LibraryReferences = 0;

		private static bool LoadFirefoxDlls()
		{
			if (Firefox.LibrariesLoaded)
			{
				Firefox.LibraryReferences++;
				return true;
			}

			lock (Firefox.LibrariesLoadedLock)
			{
				if (Firefox.LibrariesLoaded)
				{
					Firefox.LibraryReferences++;
					return true;
				}

				string ProgramFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
				string ProgramFilesPathx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
				string FirefoxPath = null;
				bool WarnMisMatch = false;

				foreach (DirectoryInfo FirefoxDir in (new IEnumerable<DirectoryInfo>[]
				{
					new DirectoryInfo(ProgramFilesPath)
						.GetDirectories()
						.Where(Dir => Dir.Name.Contains("Mozilla Firefox")).Reverse(),
					Directory.Exists(ProgramFilesPathx86)
						? new DirectoryInfo(ProgramFilesPathx86)
							.GetDirectories()
							.Where(Dir => Dir.Name.Contains("Mozilla Firefox")).Reverse()
						: new DirectoryInfo[0]
				}).SelectMany(Dirs => Dirs))
				{
					if (!File.Exists(Path.Combine(FirefoxDir.FullName, "nss3.dll")))
						continue;

					if (Utilities.Utilities.IsDllArchMismatch("nss3.dll", FirefoxDir.FullName, IntPtr.Size == 8))
					{
						WarnMisMatch = true;
						continue;
					}

					WarnMisMatch = false;
					FirefoxPath = FirefoxDir.FullName;
					break;
				}

				if (FirefoxPath == null)
				{
					foreach (string SoftwareKey in new string[] { Firefox.UninstallKey, Firefox.UninstallKey32 })
					{
						using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(SoftwareKey, false))
						{
							if (rk == null)
								continue;

							foreach (string skName in rk.GetSubKeyNames().Reverse())
							{
								using (RegistryKey sk = rk.OpenSubKey(skName))
								{
									string Name = sk.GetValue("DisplayName") as string;

									if (Name == null || !Name.Contains("Mozilla Firefox"))
										continue;

									string Location = sk.GetValue("InstallLocation") as string;

									if (Location == null || !File.Exists(Path.Combine(Location, "nss3.dll")))
										continue;

									if (Utilities.Utilities.IsDllArchMismatch("nss3.dll", Location, IntPtr.Size == 8))
									{
										WarnMisMatch = true;
										continue;
									}

									WarnMisMatch = false;
									FirefoxPath = Location;
									break;
								}
							}
						}

						if (FirefoxPath != null)
							break;
					}
				}

				if (WarnMisMatch)
				{
					Utilities.Utilities.Log("Firefox.LoadFirefoxDlls() failed, unable to load Firefox dlls, a {0} bit version of Firefox is installed but Windows Data Visualizer is a {1} bit program.",
						(IntPtr.Size == 8) ? "32" : "64",
						(IntPtr.Size == 8) ? "64" : "32");
				}

				if (FirefoxPath == null)
					return false;

				try
				{
					Firefox.mozglue = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "mozglue.dll"));
					Firefox.mozcrt19 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "mozcrt19.dll"));
					Firefox.nspr4 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "nspr4.dll"));
					Firefox.plc4 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "plc4.dll"));
					Firefox.plds4 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "plds4.dll"));
					Firefox.ssutil3 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "ssutil3.dll"));
					Firefox.sqlite3 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "sqlite3.dll"));
					Firefox.mozsqlite3 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "mozsqlite3.dll"));
					Firefox.nssutil3 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "nssutil3.dll"));
					Firefox.softokn3 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "softokn3.dll"));
					Firefox.NSS3 = Utilities.Win32.LoadLibrary(Path.Combine(FirefoxPath, "nss3.dll"));

					if (Firefox.NSS3 == IntPtr.Zero)
						return false;

					IntPtr NSSInitProc = Utilities.Win32.GetProcAddress(Firefox.NSS3, "NSS_Init");
					IntPtr NSSBase64DecodeBufferProc = Utilities.Win32.GetProcAddress(Firefox.NSS3, "NSSBase64_DecodeBuffer");
					IntPtr PK11SDRDecryptProc = Utilities.Win32.GetProcAddress(Firefox.NSS3, "PK11SDR_Decrypt");
					IntPtr PK11GetInternalKeySlotProc = Utilities.Win32.GetProcAddress(Firefox.NSS3, "PK11_GetInternalKeySlot");
					IntPtr PK11AuthenticateProc = Utilities.Win32.GetProcAddress(Firefox.NSS3, "PK11_Authenticate");
					IntPtr NSSShutdownProc = Utilities.Win32.GetProcAddress(Firefox.NSS3, "NSS_Shutdown");

					if (NSSInitProc == IntPtr.Zero
						|| PK11GetInternalKeySlotProc == IntPtr.Zero
						|| PK11AuthenticateProc == IntPtr.Zero
						|| NSSBase64DecodeBufferProc == IntPtr.Zero
						|| PK11SDRDecryptProc == IntPtr.Zero
						|| NSSShutdownProc == IntPtr.Zero)
						return false;

					Firefox.NSSInit = (NSSInitPrototype)Marshal.GetDelegateForFunctionPointer(NSSInitProc, typeof(NSSInitPrototype));
					Firefox.PK11GetInternalKeySlot = (PK11GetInternalKeySlotPrototype)Marshal.GetDelegateForFunctionPointer(PK11GetInternalKeySlotProc, typeof(PK11GetInternalKeySlotPrototype));
					Firefox.PK11Authenticate = (PK11AuthenticatePrototype)Marshal.GetDelegateForFunctionPointer(PK11AuthenticateProc, typeof(PK11AuthenticatePrototype));
					Firefox.NSSBase64DecodeBuffer = (NSSBase64DecodeBufferPrototype)Marshal.GetDelegateForFunctionPointer(NSSBase64DecodeBufferProc, typeof(NSSBase64DecodeBufferPrototype));
					Firefox.PK11SDRDecrypt = (PK11SDRDecryptPrototype)Marshal.GetDelegateForFunctionPointer(PK11SDRDecryptProc, typeof(PK11SDRDecryptPrototype));
					Firefox.NSSShutdown = (NSSShutdownPrototype)Marshal.GetDelegateForFunctionPointer(NSSShutdownProc, typeof(NSSShutdownPrototype));

					return Firefox.LibrariesLoaded = (NSSInitProc != IntPtr.Zero && Firefox.NSSInit != null);
				}
				catch (Exception e)
				{
					Utilities.Utilities.Log(e);
					return Firefox.LibrariesLoaded;
				}
				finally
				{
					if (Firefox.LibrariesLoaded)
						Firefox.LibraryReferences++;
				}
			}
		}

		private static bool FreeFirefoxDlls()
		{
			if (!Firefox.LibrariesLoaded)
				return true;

			if (--Firefox.LibraryReferences > 0)
				return true;

			if (Firefox.mozglue != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.mozglue);
				Firefox.mozglue = IntPtr.Zero;
			}

			if (Firefox.mozcrt19 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.mozcrt19);
				Firefox.mozcrt19 = IntPtr.Zero;
			}

			if (Firefox.nspr4 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.nspr4);
				Firefox.nspr4 = IntPtr.Zero;
			}

			if (Firefox.plc4 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.plc4);
				Firefox.plc4 = IntPtr.Zero;
			}

			if (Firefox.plds4 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.plds4);
				Firefox.plds4 = IntPtr.Zero;
			}

			if (Firefox.ssutil3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.ssutil3);
				Firefox.ssutil3 = IntPtr.Zero;
			}

			if (Firefox.sqlite3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.sqlite3);
				Firefox.sqlite3 = IntPtr.Zero;
			}

			if (Firefox.mozsqlite3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.mozsqlite3);
				Firefox.mozsqlite3 = IntPtr.Zero;
			}

			if (Firefox.nssutil3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.nssutil3);
				Firefox.nssutil3 = IntPtr.Zero;
			}

			if (Firefox.softokn3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.softokn3);
				Firefox.softokn3 = IntPtr.Zero;
			}

			if (Firefox.NSS3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Firefox.NSS3);
				Firefox.NSS3 = IntPtr.Zero;
			}

			return !(Firefox.LibrariesLoaded = false);
		}
		#endregion

		private static DateTime UnixEpoch = new DateTime(1970, 1, 1);

		private static class Helpers
		{
			#region Bookmarks
			public class Bookmark
			{
				public static Bookmark Create(string Path)
				{
					return new JsonSerializer().Deserialize<Bookmark>(new JsonTextReader(new StreamReader(File.OpenRead(Path))));
				}

				[JsonProperty("title")]
				public string Title { get; set; }

				[JsonProperty("id")]
				public long Id { get; set; }

				[JsonProperty("parent")]
				public long Parent { get; set; }

				[JsonProperty("dateAdded")]
				public long DateAdded { get; set; }

				[JsonProperty("lastModified")]
				public long LastModified { get; set; }

				[JsonProperty("annos")]
				public BookmarkAnos[] Annos { get; set; }

				[JsonProperty("type")]
				public string Type { get; set; }

				[JsonProperty("uri")]
				public string Uri { get; set; }

				[JsonProperty("charset")]
				public string Charset { get; set; }

				[JsonProperty("root")]
				public string Root { get; set; }

				[JsonProperty("children")]
				public Bookmark[] Children { get; set; }
			}

			public class BookmarkAnos
			{
				[JsonProperty("name")]
				public string Name { get; set; }

				[JsonProperty("flags")]
				public long Flags { get; set; }

				[JsonProperty("expires")]
				public long Expires { get; set; }

				[JsonProperty("mimeType")]
				public string MimeType { get; set; }

				[JsonProperty("type")]
				public long Type { get; set; }

				[JsonProperty("value")]
				public string Value { get; set; }
			}
			#endregion

			#region CurrentSession
			public class Session
			{
				public static Session Create(string Path)
				{
					return new JsonSerializer().Deserialize<Session>(new JsonTextReader(new StreamReader(File.OpenRead(Path))));
				}

				[JsonProperty("windows")]
				public SessionWindow[] Windows { get; set; }
			}

			public class SessionWindow
			{
				[JsonProperty("tabs")]
				public SessionTab[] Tabs { get; set; }

				[JsonProperty("title")]
				public string Title { get; set; }
			}

			public class SessionTab
			{
				[JsonProperty("entries")]
				public SessionTabEntry[] Entries { get; set; }

				[JsonProperty("index")]
				public int Index { get; set; }

				[JsonProperty("pinned")]
				public bool Pinned { get; set; }

				[JsonProperty("userTypedValue")]
				public string UserTypedValue { get; set; }
			}

			public class SessionTabEntry
			{
				[JsonProperty("url")]
				public string URL { get; set; }

				[JsonProperty("title")]
				public string Title { get; set; }

				[JsonProperty("referrer")]
				public string Referrer { get; set; }

				[JsonProperty("formdata")]
				public SesstionTabFormData FormData { get; set; }
			}

			public class SesstionTabFormData
			{
				[JsonProperty("#sessionData")]
				public Session SessionData { get; set; }

				[JsonProperty("id")]
				public SessionTabFormDataID ID { get; set; }
			}

			public class SessionTabFormDataID
			{
				[JsonProperty("sessionData")]
				public Session SessionData { get; set; }
			}
			#endregion
		}
	}
}