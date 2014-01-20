using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class Chrome : IData<Chrome>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public Chrome Initiate()
		{
			DirectoryInfo ProfilesPath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"));
			DirectoryInfo SXSProfilesPath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome SXS", "User Data"));

			IEnumerable<Profile> Profiles = ProfilesPath.Exists
				? ProfilesPath
					.GetDirectories()
					.Where(dir => File.Exists(Path.Combine(dir.FullName, "History")))
					.Select(dir => new Profile(dir).Initiate())
				: Enumerable.Empty<Profile>();

			if (SXSProfilesPath.Exists)
				Profiles = Profiles.Concat(SXSProfilesPath
					.GetDirectories()
					.Where(dir => File.Exists(Path.Combine(dir.FullName, "History")))
					.Select(dir => new Profile(dir).Initiate()));

			this.Profiles = Profiles.Memoize().AsSerializable();
			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Chrome"
				: string.Format("Chrome ({0})", this.Profiles.Count());
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
			public IEnumerable<BookmarkMenu> Bookmarks { get; private set; }
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
			public IEnumerable<TopSite> TopSites { get; private set; }
			[DataMember]
			public IEnumerable<Window> CurrentSession { get; private set; }
			[DataMember]
			public IEnumerable<Window> LastSession { get; private set; }

			public Profile Initiate()
			{
				this.Bookmarks = Extensions.DeferExecutionOnce(Chrome.GetBookmarks, this.ProfilePath).AsSerializable();
				this.Cookies = Extensions.DeferExecutionOnce(Chrome.GetCookies, this.ProfilePath).AsSerializable();
				this.Downloads = Extensions.DeferExecutionOnce(Chrome.GetDownloads, this.ProfilePath).AsSerializable();
				this.FormHistory = Extensions.DeferExecutionOnce(Chrome.GetFormHistory, this.ProfilePath).AsSerializable();
				this.History = Extensions.DeferExecutionOnce(Chrome.GetHistory, this.ProfilePath).AsSerializable();
				this.Passwords = Extensions.DeferExecutionOnce(Chrome.GetPasswords, this.ProfilePath).AsSerializable();
				this.TopSites = Extensions.DeferExecutionOnce(Chrome.GetTopSites, this.ProfilePath).AsSerializable();
				this.CurrentSession = Extensions.DeferExecutionOnce(Chrome.GetWindows, this.ProfilePath, "Current Session").AsSerializable();
				this.LastSession = Extensions.DeferExecutionOnce(Chrome.GetWindows, this.ProfilePath, "Last Session").AsSerializable();

				return this;
			}
			
			public override string ToString()
			{
				return this.ProfilePath.FullName;
			}
		}

		[DataContract]
		public sealed class BookmarkMenu : IData
		{
			[DataMember]
			public string Name { get; set; }
			[DataMember]
			public DateTime DateAdded { get; set; }
			[DataMember]
			public DateTime DateModified { get; set; }
			[DataMember]
			public IEnumerable<Bookmark> Bookmarks { get; set; }

			public override string ToString()
			{
				return string.Format("{0} ({1})", this.Name, this.Bookmarks.Count());
			}
		}

		[DataContract]
		public sealed class Bookmark : IData
		{
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			public DateTime DateAdded { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Title)
					? this.Url.ToString()
					: string.Format("{0} ({1})", this.Title, this.Url);
			}
		}

		[DataContract]
		public sealed class Cookie : IData
		{
			[DataMember]
			public string Name { get; set; }
			[DataMember]
			public string Value { get; set; }
			[DataMember]
			public string Host { get; set; }
			[DataMember]
			public string Path { get; set; }
			[DataMember]
			public DateTime Created { get; set; }
			[DataMember]
			public DateTime Expiry { get; set; }
			[DataMember]
			public bool IsHttpOnly { get; set; }
			[DataMember]
			public bool IsSecure { get; set; }
			[DataMember]
			public DateTime LastAccessed { get; set; }
			
			public override string ToString()
			{
				return string.Format("{0} ({1})", this.Name, this.Host);
			}
		}

		[DataContract]
		public sealed class Download : IData
		{
			[DataMember]
			public string Name { get; set; }
			[DataMember]
			public Uri SourceUrl { get; set; }
			[DataMember]
			public Uri TargetUrl { get; set; }
			[DataMember]
			public string MimeType { get; set; }
			[DataMember]
			public DateTime Started { get; set; }
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
			public string Name { get; set; }
			[DataMember]
			public string Value { get; set; }
			[DataMember]
			public long TimesUsed { get; set; }

			public override string ToString()
			{
				return this.Name;
			}
		}

		[DataContract]
		public sealed class History : IData
		{
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			public DateTime LastVisit { get; set; }
			[DataMember]
			public long Typed { get; set; }
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
			public string Username { get; set; }
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public string UsernameField { get; set; }
			[DataMember]
			public string PasswordField { get; set; }
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri FormSubmitUrl { get; set; }
			[DataMember]
			public DateTime Created { get; set; }
			[DataMember]
			public string Hostname { get; set; }

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
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			public DateTime LastUpdated { get; set; }
			[DataMember]
			public long Rank { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Title)
					? this.Url.ToString()
					: string.Format("{0} ({1})", this.Title, this.Url);
			}
		}

		[DataContract]
		public sealed class Window : IData
		{
			[DataMember]
			public IEnumerable<Tab> Tabs { get; set; }

			public override string ToString()
			{
				return string.Format("Window ({0})", this.Tabs.Count());
			}
		}

		[DataContract]
		public sealed class Tab : IData
		{
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			public bool Pinned { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Title)
					? this.Url.ToString()
					: string.Format("{0} ({1})", this.Title, this.Url);
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<BookmarkMenu> GetBookmarks(DirectoryInfo ProfilePath)
		{
			string BookmarksPath = Path.Combine(ProfilePath.FullName, "Bookmarks");

			if (!File.Exists(BookmarksPath))
				return Enumerable.Empty<BookmarkMenu>();

			try
			{
				Helpers.Bookmarks Bookmarks = Helpers.Bookmarks.Create(BookmarksPath);
				return Extensions.ReturnEnumerable(Bookmarks.Roots.BookmarkBar, Bookmarks.Roots.Other).Select(BookmarkMenu =>
				{
					long TempLong;
					Uri TempUri;
					return new BookmarkMenu
					{
						Bookmarks = BookmarkMenu.Children.Select(Bookmark => new Bookmark
						{
							DateAdded = long.TryParse(Bookmark.DateAdded, out TempLong)
								? Chrome.ChromeTimeOffset.AddTicks(TempLong)
								: default(DateTime),
							Title = Bookmark.Name,
							Url = Uri.TryCreate(Bookmark.Url, UriKind.Absolute, out TempUri) ? TempUri : null
						}).Memoize().AsSerializable(),
						DateAdded = long.TryParse(BookmarkMenu.DateAdded, out TempLong)
							? Chrome.ChromeTimeOffset.AddTicks(TempLong)
							: default(DateTime),
						DateModified = long.TryParse(BookmarkMenu.DateModified, out TempLong)
							? Chrome.ChromeTimeOffset.AddTicks(TempLong)
							: default(DateTime),
						Name = BookmarkMenu.Name
					};
				});
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<BookmarkMenu>();
			}
		}

		private static IEnumerable<Cookie> GetCookies(DirectoryInfo ProfilePath)
		{
			string CookiesPath = Path.Combine(ProfilePath.FullName, "Cookies");

			if (!File.Exists(CookiesPath))
				return Enumerable.Empty<Cookie>();

			List<Cookie> rData = new List<Cookie>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(CookiesPath))
				{
					Connection.ForEach("SELECT creation_utc, host_key, name, value, path, expires_utc, secure, httponly, last_access_utc FROM cookies", DataReader =>
					{
						//creation_utc, host_key, name, value, path, expires_utc, secure, httponly, last_access_utc
						long TempLong;
						rData.Add(new Cookie
						{
							Created = long.TryParse(DataReader["creation_utc"].ToString(), out TempLong)
								? Chrome.ChromeTimeOffset.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							Expiry = long.TryParse(DataReader["expires_utc"].ToString(), out TempLong)
								? Chrome.ChromeTimeOffset.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							Host = DataReader["host_key"].ToString(),
							IsHttpOnly = DataReader["httponly"].ToString() == "1",
							IsSecure = DataReader["secure"].ToString() == "1",
							LastAccessed = long.TryParse(DataReader["last_access_utc"].ToString(), out TempLong)
								? Chrome.ChromeTimeOffset.AddMilliseconds(TempLong / 1000)
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
			string DownloadsPath = Path.Combine(ProfilePath.FullName, "History");

			if (!File.Exists(DownloadsPath))
				return Enumerable.Empty<Download>();

			List<Download> rData = new List<Download>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(DownloadsPath))
				{
					Connection.ForEach("SELECT url, full_path, start_time FROM downloads", DataReader =>
					{
						//id, full_path, url, start_time, received_bytes, total_bytes, state
						bool IsTempFile = Path.GetExtension(DataReader["full_path"].ToString()) == ".crdownload"
							&& DataReader["full_path"].ToString().StartsWith("unconfirmed ");

						Uri TempUri;
						Uri TargetUrl = Uri.TryCreate(DataReader["full_path"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null;

						long TempLong;
						rData.Add(new Download
						{
							MimeType = IsTempFile
								? Utilities.Utilities.GetMimeType(DataReader["url"].ToString())
								: Utilities.Utilities.GetMimeType(DataReader["full_path"].ToString()),
							Name = IsTempFile
								? Path.GetFileName(DataReader["url"].ToString())
								: Path.GetFileName(DataReader["full_path"].ToString()),
							SourceUrl = Uri.TryCreate(DataReader["url"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null,
							Started = long.TryParse(DataReader["start_time"].ToString(), out TempLong)
								? Chrome.ChromeTimeOffset.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							TargetUrl = IsTempFile ? null : TargetUrl,
							TemporaryFile = IsTempFile ? TargetUrl : null
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
			string FormHistoryPath = Path.Combine(ProfilePath.FullName, "Web Data");

			if (!File.Exists(FormHistoryPath))
				return Enumerable.Empty<FormHistory>();
			
			List<FormHistory> rData = new List<FormHistory>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(FormHistoryPath))
				{
					Connection.ForEach("SELECT name, count, value FROM autofill", DataReader =>
					{
						//name, value, value_lower, pair_id, count
						long TempLong;
						rData.Add(new FormHistory
						{
							Name = DataReader["name"].ToString(),
							TimesUsed = long.TryParse(DataReader["count"].ToString(), out TempLong) ? TempLong : 0,
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
			string HistoryPath = Path.Combine(ProfilePath.FullName, "History");

			if (!File.Exists(HistoryPath))
				return Enumerable.Empty<History>();

			List<History> rData = new List<History>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(HistoryPath))
				{
					Connection.ForEach("SELECT last_visit_time, title, url, visit_count, typed_count FROM urls", DataReader =>
					{
						//id, url, title, visit_count, typed_count, last_visit_time, hidden, favicon_id
						long TempLong;
						Uri TempUri;
						rData.Add(new History
						{
							LastVisit = long.TryParse(DataReader["last_visit_time"].ToString(), out TempLong)
								? Chrome.ChromeTimeOffset.AddTicks(TempLong)
								: default(DateTime),
							Title = DataReader["title"].ToString(),
							Typed = long.TryParse(DataReader["typed_count"].ToString(), out TempLong) ? TempLong : -1,
							Url = Uri.TryCreate(DataReader["url"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null,
							Visits = long.TryParse(DataReader["visit_count"].ToString(), out TempLong) ? TempLong : -1
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

		private static IEnumerable<StoredPassword> GetPasswords(DirectoryInfo ProfilePath)
		{
			string LoginDataPath = Path.Combine(ProfilePath.FullName, "Login Data");

			if (!File.Exists(LoginDataPath))
				return Enumerable.Empty<StoredPassword>();

			List<StoredPassword> rData = new List<StoredPassword>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(LoginDataPath))
				{
					Connection.ForEach("SELECT action_url, username_element, username_value, password_element, password_value, date_created FROM logins", DataReader =>
					{
						//origin_url, action_url, username_element, username_value, password_element, password_value, submit_element, signon_realm, ssl_valid, preferred, date_created, blacklisted_by_user, scheme
						byte[] plainTextBytes = ProtectedData.Unprotect(DataReader["password_value"] as byte[], null, DataProtectionScope.CurrentUser);

						Uri ActionUri;
						bool ActionUriSucces = Uri.TryCreate(DataReader["action_url"].ToString(), UriKind.Absolute, out ActionUri);

						long TempLong;
						rData.Add(new StoredPassword
						{
							Created = long.TryParse(DataReader["date_created"].ToString(), out TempLong)
								? Chrome.ChromeTimeOffset.AddTicks(TempLong)
								: default(DateTime),
							Hostname = ActionUriSucces ? ActionUri.Host : null,
							FormSubmitUrl = ActionUriSucces ? ActionUri : null,
							Password = Encoding.UTF8.GetString(plainTextBytes),
							PasswordField = DataReader["password_element"].ToString(),
							Username = DataReader["username_value"].ToString(),
							UsernameField = DataReader["username_element"].ToString()
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

		private static IEnumerable<TopSite> GetTopSites(DirectoryInfo ProfilePath)
		{
			string TopSitesPath = Path.Combine(ProfilePath.FullName, "Top Sites");

			if (!File.Exists(TopSitesPath))
				return Enumerable.Empty<TopSite>();

			List<TopSite> rData = new List<TopSite>();

			try
			{
				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(TopSitesPath))
				{
					Connection.ForEach("SELECT url, url_rank, title, last_updated FROM thumbnails", DataReader =>
					{
						//url, url_rank, title, thumbnail, redirects, boring_score, good_clipping, at_top, last_updated, load_completed
						Uri TempUri;
						long TempLong;
						rData.Add(new TopSite
						{
							LastUpdated = long.TryParse(DataReader["last_updated"].ToString(), out TempLong)
								? Chrome.ChromeTimeOffset.AddMilliseconds(TempLong / 1000)
								: default(DateTime),
							Rank = long.TryParse(DataReader["url_rank"].ToString(), out TempLong) ? TempLong : 0,
							Title = DataReader["title"].ToString(),
							Url = Uri.TryCreate(DataReader["url"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null
						});
					});
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			
			rData.Sort((a, b) => a.Rank.CompareTo(b.Rank));
			return rData;
		}

		private static IEnumerable<Window> GetWindows(DirectoryInfo ProfilePath, string SessionFile)
		{
			// https://src.chromium.org/viewvc/chrome/trunk/src/chrome/browser/sessions/session_backend.cc?view=markup
			// https://src.chromium.org/viewvc/chrome/trunk/src/chrome/browser/sessions/session_service.cc?view=markup
			// https://src.chromium.org/viewvc/chrome/trunk/src/chrome/browser/sessions/base_session_service.cc?view=markup

			string SessionPath = Path.Combine(ProfilePath.FullName, SessionFile);

			if (!File.Exists(SessionPath))
				return Enumerable.Empty<Window>();

			string TempFile = Path.GetTempFileName();
			Dictionary<uint, Helpers.Window> Windows = new Dictionary<uint, Helpers.Window>();
			Dictionary<uint, Helpers.Tab> Tabs = new Dictionary<uint, Helpers.Tab>();

			try
			{
				File.Copy(SessionPath, TempFile, true);
				
				using (FileStream FS = new FileStream(TempFile, FileMode.Open, FileAccess.Read, FileShare.None))
				using (BinaryReader BR = new BinaryReader(FS))
				{
					if (BR.ReadUInt32() != Chrome.kFileSignature)
						throw new Exception("Invalid file header");

					int Version = BR.ReadInt32();

					if (Version != 1)
						throw new Exception("Invalid file version");

					while (FS.Length > FS.Position)
					{
						if (FS.Length - FS.Position < 2)
							throw new Exception("SessionFileReader::ReadCommand, file incomplete");

						// Type for writing the size. - uint16
						// Size of idType is included in commandSize
						ushort commandSize = BR.ReadUInt16();
						long startPosition = FS.Position;

						if (commandSize == 0)
							throw new Exception("SessionFileReader::ReadCommand, empty command");

						// Type for the identifier. - uint8
						byte idType = BR.ReadByte();

						switch (idType)
						{
							case 0: // kCommandSetTabWindow
								Helpers.Tab tab = new Helpers.Tab
								{
									WindowId = BR.ReadUInt32(),
									TabId = BR.ReadUInt32()
								};

								if (!Windows.ContainsKey(tab.WindowId)
									|| Tabs.ContainsKey(tab.TabId))
									break;

								Windows[tab.WindowId].Tabs.Add(tab);
								Tabs.Add(tab.TabId, tab);
								break;
							case 1: // kCommandSetWindowBounds
								// OBSOLETE Superseded by kCommandSetWindowBounds3.
								break;
							case 2: // kCommandSetTabIndexInWindow
								uint tabId = BR.ReadUInt32();

								if (!Tabs.ContainsKey(tabId))
									break;

								Tabs[tabId].IndexInWindow = BR.ReadUInt32();
								break;
							case 3: // kCommandTabClosedObsolete
								// Original kCommandTabClosed/kCommandWindowClosed. See comment in
								// MigrateClosedPayload for details on why they were replaced.
								break;
							case 4: // kCommandWindowClosedObsolete
								break;
							case 5: // kCommandTabNavigationPathPrunedFromBack
								break;
							case 6: // kCommandUpdateTabNavigation
								Helpers.Pickle Pickle = new Helpers.Pickle(BR);
								Helpers.TabNavigation tabNavigation = new Helpers.TabNavigation
								{
									TabId = Pickle.ReadUInt32(),
									Index = Pickle.ReadUInt32(),
									Url = Pickle.ReadString(),
									Title = Pickle.ReadString16()
								};

								if (!Tabs.ContainsKey(tabNavigation.TabId))
									break;

								Tabs[tabNavigation.TabId].TabNavigation[tabNavigation.Index] = tabNavigation;
								break;
							case 7: // kCommandSetSelectedNavigationIndex
								tabId = BR.ReadUInt32();

								if (!Tabs.ContainsKey(tabId))
									break;

								Tabs[tabId].SelectedUrl = BR.ReadUInt32() & 0xFF;
								break;
							case 8: // kCommandSetSelectedTabInIndex
								break;
							case 9: // kCommandSetWindowType
								Helpers.Window window = new Helpers.Window
								{
									WindowId = BR.ReadUInt32()
								};

								if (Windows.ContainsKey(window.WindowId))
									break;

								Windows.Add(window.WindowId, window);
								break;
							case 10: // kCommandSetWindowBounds2
								// OBSOLETE Superseded by kCommandSetWindowBounds3. Except for data migration.
								break;
							case 11: // kCommandTabNavigationPathPrunedFromFront
								break;
							case 12: // kCommandSetPinnedState
								tabId = BR.ReadUInt32();

								if (!Tabs.ContainsKey(tabId))
									break;

								Tabs[tabId].Pinned = BR.ReadUInt32() != 0;
								break;
							case 13: // kCommandSetExtensionAppID
								break;
							case 14: // kCommandSetWindowBounds3
								break;
							case 15: // kCommandSetWindowAppName
								break;
							case 16: // kCommandTabClosed
								break;
							case 17: // kCommandWindowClosed
								break;
							case 18: // kCommandSetTabUserAgentOverride
								break;
							case 19: // kCommandSessionStorageAssociated
								break;
							default:
								Utilities.Utilities.Log("Chrome.GetWindows: Unkown command: {0}", idType);
								break;
						}

						FS.Seek(startPosition + commandSize, SeekOrigin.Begin);
					}
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			finally
			{
				File.Delete(TempFile);
			}
			
			return Windows.Select(Window =>
			{
				Window.Value.Tabs.Sort((a, b) => a.IndexInWindow.CompareTo(b.IndexInWindow));

				return new Window
				{
					Tabs = Window.Value.Tabs
						.Where(tab => tab.SelectedUrl < tab.TabNavigation.Count)
						.Select(tab =>
						{
							Uri TempUri;
							return new Tab
							{
								Pinned = tab.Pinned,
								Title = tab.TabNavigation[tab.SelectedUrl].Title,
								Url = Uri.TryCreate(tab.TabNavigation[tab.SelectedUrl].Url, UriKind.Absolute, out TempUri) ? TempUri : null
							};
						}).Memoize().AsSerializable()
				};
			});
		}
		#endregion

		// https://src.chromium.org/viewvc/chrome/trunk/src/chrome/browser/sessions/session_backend.cc?view=markup
		// The signature at the beginning of the file = SSNS (Sessions).
		private const uint kFileSignature = 0x53534E53;

		private static DateTime ChromeTimeOffset = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Local);

		private static class Helpers
		{
			#region Session
			public class Window
			{
				public uint WindowId;
				public List<Tab> Tabs = new List<Tab>();
			}

			public class Tab
			{
				public uint WindowId;
				public uint TabId;
				public Dictionary<uint, TabNavigation> TabNavigation = new Dictionary<uint, TabNavigation>();
				public uint SelectedUrl;
				public uint IndexInWindow;
				public bool Pinned;
			}

			public class TabNavigation
			{
				public uint TabId;
				public uint Index;
				public string Url;
				public string Title;
			}
			#endregion

			// https://src.chromium.org/viewvc/chrome/trunk/src/chrome/browser/sessions/pickle.cc?view=markup
			public sealed class Pickle
			{
				public Pickle(BinaryReader BR)
				{
					this.BR = BR;

					this.payloadSize = BR.ReadUInt32();

					this.startReadPointer = BR.BaseStream.Position;
					this.endReadPointer = this.startReadPointer + this.payloadSize;
				}

				private BinaryReader BR;
				private long startReadPointer;
				private long readPointer { get { return this.BR.BaseStream.Position; } set { this.BR.BaseStream.Seek(value, SeekOrigin.Begin); } }
				private long endReadPointer;
				private uint payloadSize;

				private long AlignInt(long i, int alignment)
				{
					return i + (alignment - (i % alignment)) % alignment;
				}

				private void AdvanceReadPointer<T>()
				{
					int SizeOfT = Marshal.SizeOf(typeof(T));

					if (SizeOfT < sizeof(int))
						this.BR.BaseStream.Seek(this.AlignInt(SizeOfT, sizeof(int)) - SizeOfT, SeekOrigin.Current);
				}

				private void AdvnaceReadPointer(int num_bytes)
				{
					this.BR.BaseStream.Seek(this.AlignInt(num_bytes, sizeof(int)) - num_bytes, SeekOrigin.Current);
				}

				public bool ReadBool()
				{
					bool Ret = this.BR.ReadBoolean();
					this.AdvanceReadPointer<bool>();
					return Ret;
				}

				public int ReadInt()
				{
					int Ret = this.BR.ReadInt32();
					this.AdvanceReadPointer<int>();
					return Ret;
				}

				public long ReadLong()
				{
					long Ret = this.BR.ReadInt64();
					this.AdvanceReadPointer<long>();
					return Ret;
				}

				public ushort ReadUInt16()
				{
					ushort Ret = this.BR.ReadUInt16();
					this.AdvanceReadPointer<ushort>();
					return Ret;
				}

				public uint ReadUInt32()
				{
					uint Ret = this.BR.ReadUInt32();
					this.AdvanceReadPointer<uint>();
					return Ret;
				}

				public long ReadInt64()
				{
					long Ret = this.BR.ReadInt64();
					this.AdvanceReadPointer<long>();
					return Ret;
				}

				public ulong ReadUInt64()
				{
					ulong Ret = this.BR.ReadUInt64();
					this.AdvanceReadPointer<ulong>();
					return Ret;
				}

				public string ReadString()
				{
					int len = this.ReadInt();
					string Ret = Encoding.UTF8.GetString(BR.ReadBytes(len), 0, len);
					this.AdvnaceReadPointer(len);
					return Ret;
				}

				public string ReadString16()
				{
					int len = this.ReadInt() * 2;
					string Ret = Encoding.Unicode.GetString(BR.ReadBytes(len), 0, len);
					this.AdvnaceReadPointer(len);
					return Ret;
				}
			}

			#region Bookmarks
			public sealed class Bookmarks
			{
				public static Bookmarks Create(string Path)
				{
					return new JsonSerializer().Deserialize<Bookmarks>(new JsonTextReader(new StreamReader(File.OpenRead(Path))));
				}

				[JsonProperty("roots")]
				public BookmarksRoot Roots { get; set; }
			}

			public sealed class BookmarksRoot
			{
				[JsonProperty("bookmark_bar")]
				public BookmarksMenu BookmarkBar { get; set; }

				[JsonProperty("other")]
				public BookmarksMenu Other { get; set; }
			}

			public sealed class BookmarksMenu
			{
				[JsonProperty("children")]
				public Bookmark[] Children { get; set; }

				[JsonProperty("date_added")]
				public string DateAdded { get; set; }

				[JsonProperty("date_modified")]
				public string DateModified { get; set; }

				[JsonProperty("name")]
				public string Name { get; set; }
			}

			public sealed class Bookmark
			{
				[JsonProperty("date_added")]
				public string DateAdded { get; set; }

				[JsonProperty("name")]
				public string Name { get; set; }

				[JsonProperty("url")]
				public string Url { get; set; }
			}
			#endregion
		}
	}
}