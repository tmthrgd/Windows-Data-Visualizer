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
	public sealed class Thunderbird : IData<Thunderbird>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public Thunderbird Initiate()
		{
			string ThunderbirdAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Thunderbird");
			string ProfilesIniPath = Path.Combine(ThunderbirdAppData, "profiles.ini");

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
						Profiles.Add(new Profile(new DirectoryInfo(Path.Combine(ThunderbirdAppData, profile.Value["Path"]))).Initiate());
					else
						Profiles.Add(new Profile(new DirectoryInfo(profile.Value["Path"])).Initiate());
				}

				this.Profiles = Profiles.Memoize().AsSerializable();
			}
			else
			{
				DirectoryInfo ThunderbirdProfilesDir = new DirectoryInfo(Path.Combine(ThunderbirdAppData, "Profiles"));

				if (ThunderbirdProfilesDir.Exists)
					this.Profiles = ThunderbirdProfilesDir.GetDirectories().Select(dir => new Profile(dir).Initiate()).Memoize().AsSerializable();
				else
					this.Profiles = Enumerable.Empty<Profile>();
			}

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Thunderbird"
				: string.Format("Thunderbird ({0})", this.Profiles.Count());
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
			public IEnumerable<StoredPassword> Passwords { get; private set; }

			public Profile Initiate()
			{
				this.Passwords = Extensions.DeferExecutionOnce(Thunderbird.GetPasswords, this.ProfilePath).AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return this.ProfilePath.FullName;
			}
		}

		[DataContract]
		public sealed class StoredPassword : IData
		{
			[DataMember]
			public DateTime Created { get; set; }
			[DataMember]
			//[Utilities.NavigateToAttribute]
			public Uri Hostname { get; set; }
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public string Username { get; set; }
			[DataMember]
			public DateTime LastChanged { get; set; }
			[DataMember]
			public DateTime LastUsed { get; set; }
			[DataMember]
			public long TimesUsed { get; set; }

			public override string ToString()
			{
				return (this.Hostname == null)
					? this.Username
					: string.Format("{0} ({1})", this.Username, this.Hostname);
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<StoredPassword> GetPasswords(DirectoryInfo ProfilePath)
		{
			if (Thunderbird.NSSInitProfile != null && Thunderbird.NSSInitProfile != ProfilePath.FullName)
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
				Thunderbird.LoadThunderbirdDlls();

				if (!Thunderbird.LibrariesLoaded)
					return Enumerable.Empty<StoredPassword>();

				Thunderbird.NSSInitProfile = ProfilePath.FullName;
				Thunderbird.NSSInit(ProfilePath.FullName);
				Thunderbird.PK11Authenticate(Thunderbird.PK11GetInternalKeySlot(), true, 0);

				using (Utilities.SQLiteHelper Connection = new Utilities.SQLiteHelper(SignonsPath))
				{
					Connection.ForEach("SELECT hostname, encryptedUsername, encryptedPassword, timeCreated, timeLastUsed, timePasswordChanged, timesUsed FROM moz_logins", DataReader =>
					{
						IntPtr hi2 = IntPtr.Zero;
						IntPtr hi22 = IntPtr.Zero;

						try
						{
							// id, hostname, httpRealm, formSubmitURL, usernameField, passwordField, encryptedUsername, encryptedPassword, guid, encType, timeCreated, timeLastUsed, timePasswordChanged, timesUsed
							string Username = null;
							string Password = null;

							StringBuilder se = new StringBuilder(DataReader["encryptedUsername"].ToString());
							hi2 = Thunderbird.NSSBase64DecodeBuffer(IntPtr.Zero, IntPtr.Zero, se, se.Length);
							TSECItem item = (TSECItem)Marshal.PtrToStructure(hi2, typeof(TSECItem));
							se.Clear();

							TSECItem tSecDec = new TSECItem();

							if (Thunderbird.PK11SDRDecrypt(ref item, ref tSecDec, 0) == 0 && tSecDec.SECItemLen != 0)
							{
								byte[] bvRet = new byte[tSecDec.SECItemLen];
								Marshal.Copy(tSecDec.SECItemData, bvRet, 0, tSecDec.SECItemLen);
								Username = Encoding.UTF8.GetString(bvRet);
								Array.Clear(bvRet, 0, bvRet.Length);
								bvRet = null;
							}

							se = new StringBuilder(DataReader["encryptedPassword"].ToString());
							hi22 = Thunderbird.NSSBase64DecodeBuffer(IntPtr.Zero, IntPtr.Zero, se, se.Length);
							item = (TSECItem)Marshal.PtrToStructure(hi22, typeof(TSECItem));
							se.Clear();

							tSecDec = new TSECItem();

							if (Thunderbird.PK11SDRDecrypt(ref item, ref tSecDec, 0) == 0 && tSecDec.SECItemLen != 0)
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
										? Thunderbird.UnixEpoch.AddMilliseconds(TempLong / 1000)
										: default(DateTime),
									LastChanged = long.TryParse(DataReader["timePasswordChanged"].ToString(), out TempLong)
										? Thunderbird.UnixEpoch.AddMilliseconds(TempLong / 1000)
										: default(DateTime),
									LastUsed = long.TryParse(DataReader["timeLastUsed"].ToString(), out TempLong)
										? Thunderbird.UnixEpoch.AddMilliseconds(TempLong / 1000)
										: default(DateTime),
									Hostname = Uri.TryCreate(DataReader["hostname"].ToString(), UriKind.Absolute, out TempUri) ? TempUri : null,
									Password = Password,
									TimesUsed = long.TryParse(DataReader["timesUsed"].ToString(), out TempLong) ? TempLong : 0,
									Username = Username
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
				if (Thunderbird.NSSShutdown != null)
					Thunderbird.NSSShutdown();

				Thunderbird.FreeThunderbirdDlls();
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
		private static IntPtr mozsqlite3 = IntPtr.Zero;
		private static IntPtr nspr4 = IntPtr.Zero;
		private static IntPtr plc4 = IntPtr.Zero;
		private static IntPtr plds4 = IntPtr.Zero;
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

		private static bool LoadThunderbirdDlls()
		{
			if (Thunderbird.LibrariesLoaded)
			{
				Thunderbird.LibraryReferences++;
				return true;
			}

			lock (Thunderbird.LibrariesLoadedLock)
			{
				if (Thunderbird.LibrariesLoaded)
				{
					Thunderbird.LibraryReferences++;
					return true;
				}

				string ProgramFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
				string ProgramFilesPathx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
				string ThunderbirdPath = null;
				bool WarnMisMatch = false;

				foreach (DirectoryInfo ThunderbirdDir in (new IEnumerable<DirectoryInfo>[]
				{
					new DirectoryInfo(ProgramFilesPath)
						.GetDirectories()
						.Where(Dir => Dir.Name.Contains("Mozilla Thunderbird")).Reverse(),
					Directory.Exists(ProgramFilesPathx86)
						? new DirectoryInfo(ProgramFilesPathx86)
							.GetDirectories()
							.Where(Dir => Dir.Name.Contains("Mozilla Thunderbird")).Reverse()
						: new DirectoryInfo[0]
				}).SelectMany(Dirs => Dirs))
				{
					if (!File.Exists(Path.Combine(ThunderbirdDir.FullName, "nss3.dll")))
						continue;

					if (Utilities.Utilities.IsDllArchMismatch("nss3.dll", ThunderbirdDir.FullName, IntPtr.Size == 8))
					{
						WarnMisMatch = true;
						continue;
					}

					WarnMisMatch = false;
					ThunderbirdPath = ThunderbirdDir.FullName;
					break;
				}

				if (ThunderbirdPath == null)
				{
					foreach (string SoftwareKey in new string[] { Thunderbird.UninstallKey, Thunderbird.UninstallKey32 })
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

									if (Name == null || !Name.Contains("Mozilla Thunderbird"))
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
									ThunderbirdPath = Location;
									break;
								}
							}
						}

						if (ThunderbirdPath != null)
							break;
					}
				}

				if (WarnMisMatch)
				{
					Utilities.Utilities.Log("Thunderbird.LoadThunderbirdDlls() failed, unable to load Thunderbird dlls, a {0} bit version of Thunderbird is installed but Windows Data Visualizer is a {1} bit program.",
						(IntPtr.Size == 8) ? "32" : "64",
						(IntPtr.Size == 8) ? "64" : "32");
				}

				if (ThunderbirdPath == null)
					return false;

				try
				{
					Thunderbird.mozglue = Utilities.Win32.LoadLibrary(Path.Combine(ThunderbirdPath, "mozglue.dll"));
					Thunderbird.mozsqlite3 = Utilities.Win32.LoadLibrary(Path.Combine(ThunderbirdPath, "mozsqlite3.dll"));
					Thunderbird.nspr4 = Utilities.Win32.LoadLibrary(Path.Combine(ThunderbirdPath, "nspr4.dll"));
					Thunderbird.plc4 = Utilities.Win32.LoadLibrary(Path.Combine(ThunderbirdPath, "plc4.dll"));
					Thunderbird.plds4 = Utilities.Win32.LoadLibrary(Path.Combine(ThunderbirdPath, "plds4.dll"));
					Thunderbird.nssutil3 = Utilities.Win32.LoadLibrary(Path.Combine(ThunderbirdPath, "nssutil3.dll"));
					Thunderbird.softokn3 = Utilities.Win32.LoadLibrary(Path.Combine(ThunderbirdPath, "softokn3.dll"));
					Thunderbird.NSS3 = Utilities.Win32.LoadLibrary(Path.Combine(ThunderbirdPath, "nss3.dll"));

					if (Thunderbird.NSS3 == IntPtr.Zero)
						return false;

					IntPtr NSSInitProc = Utilities.Win32.GetProcAddress(Thunderbird.NSS3, "NSS_Init");
					IntPtr NSSBase64DecodeBufferProc = Utilities.Win32.GetProcAddress(Thunderbird.NSS3, "NSSBase64_DecodeBuffer");
					IntPtr PK11SDRDecryptProc = Utilities.Win32.GetProcAddress(Thunderbird.NSS3, "PK11SDR_Decrypt");
					IntPtr PK11GetInternalKeySlotProc = Utilities.Win32.GetProcAddress(Thunderbird.NSS3, "PK11_GetInternalKeySlot");
					IntPtr PK11AuthenticateProc = Utilities.Win32.GetProcAddress(Thunderbird.NSS3, "PK11_Authenticate");
					IntPtr NSSShutdownProc = Utilities.Win32.GetProcAddress(Thunderbird.NSS3, "NSS_Shutdown");

					if (NSSInitProc == IntPtr.Zero
						|| PK11GetInternalKeySlotProc == IntPtr.Zero
						|| PK11AuthenticateProc == IntPtr.Zero
						|| NSSBase64DecodeBufferProc == IntPtr.Zero
						|| PK11SDRDecryptProc == IntPtr.Zero
						|| NSSShutdownProc == IntPtr.Zero)
						return false;

					Thunderbird.NSSInit = (NSSInitPrototype)Marshal.GetDelegateForFunctionPointer(NSSInitProc, typeof(NSSInitPrototype));
					Thunderbird.PK11GetInternalKeySlot = (PK11GetInternalKeySlotPrototype)Marshal.GetDelegateForFunctionPointer(PK11GetInternalKeySlotProc, typeof(PK11GetInternalKeySlotPrototype));
					Thunderbird.PK11Authenticate = (PK11AuthenticatePrototype)Marshal.GetDelegateForFunctionPointer(PK11AuthenticateProc, typeof(PK11AuthenticatePrototype));
					Thunderbird.NSSBase64DecodeBuffer = (NSSBase64DecodeBufferPrototype)Marshal.GetDelegateForFunctionPointer(NSSBase64DecodeBufferProc, typeof(NSSBase64DecodeBufferPrototype));
					Thunderbird.PK11SDRDecrypt = (PK11SDRDecryptPrototype)Marshal.GetDelegateForFunctionPointer(PK11SDRDecryptProc, typeof(PK11SDRDecryptPrototype));
					Thunderbird.NSSShutdown = (NSSShutdownPrototype)Marshal.GetDelegateForFunctionPointer(NSSShutdownProc, typeof(NSSShutdownPrototype));

					return Thunderbird.LibrariesLoaded = (NSSInitProc != IntPtr.Zero && Thunderbird.NSSInit != null);
				}
				catch (Exception e)
				{
					Utilities.Utilities.Log(e);
					return Thunderbird.LibrariesLoaded;
				}
				finally
				{
					if (Thunderbird.LibrariesLoaded)
						Thunderbird.LibraryReferences++;
				}
			}
		}

		private static bool FreeThunderbirdDlls()
		{
			if (!Thunderbird.LibrariesLoaded)
				return true;

			if (--Thunderbird.LibraryReferences > 0)
				return true;

			if (Thunderbird.mozglue != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Thunderbird.mozglue);
				Thunderbird.mozglue = IntPtr.Zero;
			}

			if (Thunderbird.mozsqlite3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Thunderbird.mozsqlite3);
				Thunderbird.mozsqlite3 = IntPtr.Zero;
			}

			if (Thunderbird.nspr4 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Thunderbird.nspr4);
				Thunderbird.nspr4 = IntPtr.Zero;
			}

			if (Thunderbird.plc4 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Thunderbird.plc4);
				Thunderbird.plc4 = IntPtr.Zero;
			}

			if (Thunderbird.plds4 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Thunderbird.plds4);
				Thunderbird.plds4 = IntPtr.Zero;
			}

			if (Thunderbird.nssutil3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Thunderbird.nssutil3);
				Thunderbird.nssutil3 = IntPtr.Zero;
			}

			if (Thunderbird.softokn3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Thunderbird.softokn3);
				Thunderbird.softokn3 = IntPtr.Zero;
			}

			if (Thunderbird.NSS3 != IntPtr.Zero)
			{
				Utilities.Win32.FreeLibrary(Thunderbird.NSS3);
				Thunderbird.NSS3 = IntPtr.Zero;
			}

			return !(Thunderbird.LibrariesLoaded = false);
		}
		#endregion

		private static DateTime UnixEpoch = new DateTime(1970, 1, 1);
	}
}