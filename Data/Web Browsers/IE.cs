using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using ComTypes = System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public class IE : IData<IE>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public IE Initiate()
		{
			this.Profiles = EnumerableEx.Return(new Profile().Initiate()).AsSerializable();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "IE"
				: string.Format("IE ({0})", this.Profiles.Count());
		}

		private string ProfilesConverter(IEnumerable<Profile> Data)
		{
			return string.Format("{0}\r\n\t{1}", Data.Count(), string.Join("\r\n\t", Data)).TrimEnd('\r', '\n', '\t');
		}

		#region Types
		[DataContract]
		public sealed class Profile : IData<Profile>
		{
			[DataMember]
			public IEnumerable<Bookmark> Bookmarks { get; private set; }
			[DataMember]
			public IEnumerable<History> History { get; private set; }
			[DataMember]
			public IEnumerable<StoredPassword> Passwords { get; private set; }

			public Profile Initiate()
			{
				this.Bookmarks = Extensions.DeferExecutionOnce(IE.GetBookmarks).AsSerializable();
				this.History = Extensions.DeferExecutionOnce(IE.GetHistory).AsSerializable();
				this.Passwords = Extensions.DeferExecutionOnce(IE.GetPasswords, this.History).AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return string.Format("{0}\\{1}", Environment.UserDomainName, Environment.UserName);
			}
		}

		[DataContract]
		public sealed class Bookmark : IData
		{
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			public IEnumerable<Bookmark> Bookmarks { get; set; }
			[DataMember]
			public DateTime DateAdded { get; set; }
			[DataMember]
			public string Title { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			[Utilities.NavigateToAttribute]
			public Uri Url { get; set; }

			public override string ToString()
			{
				return (this.Url == null)
					? this.Title
					: string.Format("{0} ({1})", this.Title, this.Url);
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
			public string Hostname { get; set; }
			[DataMember]
			[Utilities.NavigateToAttribute]
			public Uri FormSubmitUrl { get; set; }
			[DataMember]
			public string Password { get; set; }
			[DataMember]
			public string Username { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(EmitDefaultValue = false)]
			public string Realm { get; set; }
			[DataMember]
			public PasswordType Type { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Hostname)
					? this.Username
					: string.Format("{0} ({1})", this.Username, this.Hostname);
			}
		}

		public enum PasswordType
		{
			HTTP,
			HTML
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Bookmark> GetBookmarks()
		{
			try
			{
				DirectoryInfo FavoritesPath = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Favorites));

				if (FavoritesPath.Exists)
					return IE.ParseFavoriteFolder(FavoritesPath);
				else
					return Enumerable.Empty<Bookmark>();
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<Bookmark>();
			}
		}

		private static IEnumerable<History> GetHistory()
		{
			List<History> rData = new List<History>();
			Helpers.UrlHistoryClass HistoryClass;
			Helpers.IUrlHistoryStg HistoryStg = null;
			Helpers.IEnumSTATURL Enumrator = null;

			try
			{
				HistoryClass = new Helpers.UrlHistoryClass();
				HistoryStg = (Helpers.IUrlHistoryStg)HistoryClass;
				Enumrator = (Helpers.IEnumSTATURL)HistoryStg.EnumUrls;
				Helpers.STATURL StatUrl = new Helpers.STATURL();
				int Index;

				do
				{
					Enumrator.Next(1, ref StatUrl, out Index);

					try
					{
						Uri TempUri;
						rData.Add(new History
						{
							LastVisit = DateTime.FromFileTime(StatUrl.ftLastVisited),
							Title = StatUrl.pwcsTitle,
							Url = Uri.TryCreate(StatUrl.pwcsUrl, UriKind.Absolute, out TempUri) ? TempUri : null
						});
					}
					catch (Exception e)
					{
						Utilities.Utilities.Log(e);
					}
				}
				while (Index != 0);
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			finally
			{
				if (Enumrator != null)
					Marshal.ReleaseComObject(Enumrator);

				if (HistoryStg != null)
					Marshal.ReleaseComObject(HistoryStg);

				HistoryClass = null;
			}

			return rData;
		}

		private static IEnumerable<StoredPassword> GetPasswords(IEnumerable<History> History)
		{
			List<StoredPassword> rData = new List<StoredPassword>();

			#region AutoComplete Passwords
			try
			{
				IEnumerable<Uri> UrisToTest = new List<Uri>()
				{
					new Uri("http://www.facebook.com/"),
					new Uri("http://facebook.com/login.php"),
					new Uri("http://www.facebook.com/login.php"),
					new Uri("http://login.facebook.com/"),
					new Uri("https://login.facebook.com/login.php"),
					new Uri("https://www.google.com/accounts/login"),
					new Uri("https://www.google.com/accounts/servicelogin"),
					new Uri("https://www.google.com/accounts/serviceloginauth"),
					new Uri("http://twitter.com/"),
					//new Uri("https://twitter.com/"),
					new Uri("https://twitter.com/login"),
					new Uri("http://twitter.com/sessions"),
					new Uri("https://login.skype.com/account/login-form")
				};
				UrisToTest = UrisToTest
					.SelectMany(UriToTest => new Uri[2]
					{
						(new UriBuilder(UriToTest) { Scheme = Uri.UriSchemeHttp }).Uri,
						(new UriBuilder(UriToTest) { Scheme = Uri.UriSchemeHttps }).Uri,
					})
					.Concat(History
						.Where(HistoryItem => HistoryItem.Url != null
							&& (HistoryItem.Url.Scheme == Uri.UriSchemeHttp
								|| HistoryItem.Url.Scheme == Uri.UriSchemeHttps))
						.Select(HistoryItem => new Uri(HistoryItem.Url.GetLeftPart(UriPartial.Path).ToLower())))
					.Distinct();

				using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(IE.Storage2Key, false))
				{
					int NumPasswords = (rk == null) ? 0 : rk.GetValueNames().Where(skName => skName.Length == IE.HashLength).Count();

					if (NumPasswords > 0)
					{
						int FoundPasswords = 0;

						using (SHA1 Hasher = (Environment.OSVersion.Version.Major >= 6) ? (SHA1)new SHA1Cng() : new SHA1CryptoServiceProvider())
						{
							foreach (Uri Url in UrisToTest)
							{
								byte[] UrlBytes = Encoding.Unicode.GetBytes(Url.OriginalString + "\0");
								StringBuilder Hash = new StringBuilder(IE.HashLength, IE.HashLength);

								try
								{
									byte[] Data = Hasher.ComputeHash(UrlBytes); // Data.Length == 20
									byte tail = 0;

									foreach (byte b in Data)
									{
										Hash.Append(b.ToString("X2"));
										tail += b;
									}

									Hash.Append(tail.ToString("X2"));
								}
								catch (Exception e)
								{
									Utilities.Utilities.Log(e);
									continue;
								}

								if (Hash.Length != IE.HashLength)
									continue;

								byte[] skValue = rk.GetValue(Hash.ToString()) as byte[];

								if (skValue == null)
									continue;

								IntPtr pbData = IntPtr.Zero;

								try
								{
									byte[] Data = ProtectedData.Unprotect(skValue, UrlBytes, DataProtectionScope.CurrentUser);

									pbData = Marshal.AllocHGlobal(Data.Length);
									Marshal.Copy(Data, 0, pbData, Data.Length);

									Helpers.IEAutoComplteSecretHeader IEAutoHeader = (Helpers.IEAutoComplteSecretHeader)Marshal.PtrToStructure(pbData, typeof(Helpers.IEAutoComplteSecretHeader));

									if (Data.Length >= (IEAutoHeader.dwSize + IEAutoHeader.dwSecretInfoSize + IEAutoHeader.dwSecretSize))
									{
										int dwTotalSecrets = IEAutoHeader.IESecretHeader.dwTotalSecrets / 2;
										IntPtr pSecEntry = new IntPtr(pbData.ToInt64() + Marshal.SizeOf(IEAutoHeader));
										IntPtr secOffset = new IntPtr(pbData.ToInt64() + IEAutoHeader.dwSize + IEAutoHeader.dwSecretInfoSize);

										for (int i = 0; i < dwTotalSecrets; i++)
										{
											Helpers.SecretEntry SecEntry = (Helpers.SecretEntry)Marshal.PtrToStructure(pSecEntry, typeof(Helpers.SecretEntry));

											string UserName = Marshal.PtrToStringAuto(new IntPtr(secOffset.ToInt64() + SecEntry.dwOffset));

											Marshal.DestroyStructure(pSecEntry, typeof(Helpers.SecretEntry));
											pSecEntry = new IntPtr(pSecEntry.ToInt64() + Marshal.SizeOf(SecEntry));

											SecEntry = (Helpers.SecretEntry)Marshal.PtrToStructure(pSecEntry, typeof(Helpers.SecretEntry));

											string Password = Marshal.PtrToStringAuto(new IntPtr(secOffset.ToInt64() + SecEntry.dwOffset));

											Marshal.DestroyStructure(pSecEntry, typeof(Helpers.SecretEntry));
											pSecEntry = new IntPtr(pSecEntry.ToInt64() + Marshal.SizeOf(SecEntry));

											rData.Add(new StoredPassword
											{
												FormSubmitUrl = Url,
												Hostname = Url.Host,
												Password = Password,
												Type = PasswordType.HTML,
												Username = UserName
											});
										}
									}
								}
								catch (Exception e)
								{
									Utilities.Utilities.Log(e);
								}
								finally
								{
									if (pbData != IntPtr.Zero)
										Marshal.FreeHGlobal(pbData);
								}

								if (++FoundPasswords == NumPasswords)
									break;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}
			#endregion

			#region HTTP Basic Auth Passwords
			IntPtr Credentials = IntPtr.Zero;

			try
			{
				// OptionalEntropy is 4 times Salt
				char[] Salt = "abe2869f-9b47-4cd9-a358-c22904dba7f7\0".ToCharArray();
				byte[] OptionalEntropy = new byte[Salt.Length * sizeof(char)];

				for (int i = 0, j = 0; i < Salt.Length && j < OptionalEntropy.Length; i++, j += 2)
					Buffer.BlockCopy(BitConverter.GetBytes((short)(Salt[i] * 4)), 0, OptionalEntropy, j, sizeof(short));

				uint Count;

				if (Utilities.Win32.CredEnumerate(null, Utilities.Win32.CredEnumerateFlags.None, out Count, out Credentials))
				{
					for (int i = 0; i < Count; i++)
					{
						Utilities.Win32.CREDENTIAL Credential = default(Utilities.Win32.CREDENTIAL);

						try
						{
							IntPtr pCredential = Marshal.ReadIntPtr(Credentials, IntPtr.Size * i);
							Credential = (Utilities.Win32.CREDENTIAL)Marshal.PtrToStructure(pCredential, typeof(Utilities.Win32.CREDENTIAL));

							if (Credential.Type != Utilities.Win32.CREDENTIAL_Type.CRED_TYPE_GENERIC
								|| !Credential.TargetName.StartsWith("Microsoft_WinInet_")
								|| Credential.CredentialBlob == IntPtr.Zero
								|| Credential.CredentialBlobSize == 0)
								continue;

							byte[] EncryptedData = new byte[Credential.CredentialBlobSize];
							Marshal.Copy(Credential.CredentialBlob, EncryptedData, 0, (int)Credential.CredentialBlobSize);

							byte[] Data = ProtectedData.Unprotect(EncryptedData, OptionalEntropy, DataProtectionScope.CurrentUser);
							string[] Target = Credential.TargetName.Substring(18) // 18 == "Mcriosoft_WinInet_".Length
								.Split(new char[] { '/' }, 2);
							string[] UserPass = Encoding.Unicode.GetString(Data, 0, Data.Length - 2) // remove null
								.Split(new char[] { ':' }, 2);

							rData.Add(new StoredPassword
							{
								Hostname = Target[0],
								Password = UserPass[1],
								Realm = Target[1],
								Type = PasswordType.HTTP,
								Username = UserPass[0]
							});
						}
						catch (Exception e)
						{
							Utilities.Utilities.Log(e);
						}
						finally
						{
							/*if (Credential.CredentialBlob != IntPtr.Zero)
								//Marshal.FreeBSTR(Credential.CredentialBlob);
								Marshal.FreeHGlobal(Credential.CredentialBlob);*/
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
			#endregion

			return rData;
		}

		private static IEnumerable<Bookmark> ParseFavoriteFolder(DirectoryInfo Folder)
		{
			List<Bookmark> rData = new List<Bookmark>();

			foreach (FileInfo Favorite in Folder.GetFiles("*.url"))
			{
				try
				{
					Utilities.IniParser Parser = new Utilities.IniParser(Favorite.FullName);
					Uri TempUri;
					rData.Add(new Bookmark
					{
						DateAdded = Favorite.CreationTime,
						Title = Path.GetFileNameWithoutExtension(Favorite.Name),
						Url = Uri.TryCreate(Parser["InternetShortcut"]["URL"], UriKind.Absolute, out TempUri) ? TempUri : null
					});
				}
				catch (Exception e)
				{
					Utilities.Utilities.Log(e);
				}
			}

			foreach (DirectoryInfo FavoriteFolder in Folder.GetDirectories())
			{
				rData.Add(new Bookmark
				{
					Bookmarks = IE.ParseFavoriteFolder(FavoriteFolder).AsSerializable(),
					DateAdded = FavoriteFolder.CreationTime,
					Title = FavoriteFolder.Name
				});
			}

			return rData;
		}
		#endregion

		private const string Storage2Key = @"SOFTWARE\Microsoft\Internet Explorer\IntelliForms\Storage2";
		private const int HashLength = 42;

		private static class Helpers
		{
			#region Passwords
			public struct IEAutoComplteSecretHeader
			{
				public int dwSize;		   //This header size
				public int dwSecretInfoSize; //= sizeof(IESecretInfoHeader) + numSecrets * sizeof(SecretEntry);
				public int dwSecretSize;	 //Size of the actual secret strings such as username & password
				public IESecretInfoHeader IESecretHeader;  //info about secrets such as count, size etc
				//public SecretEntry[] secEntries; //Header for each Secret String
				//public string[] secrets;		  //Actual Secret String in Unicode
			}

			public struct IESecretInfoHeader
			{
				public int dwIdHeader;	 // value - 57 49 43 4B
				public int dwSize;		 // size of this header....24 bytes
				public int dwTotalSecrets; // divide this by 2 to get actual website entries
				public int unknown;
				public int id4;			// value - 01 00 00 00
				public int unknownZero;
			}

			public struct SecretEntry
			{
				public int dwOffset;	//Offset of this secret entry from the start of secret entry strings
				[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
				public byte[] SecretId; //UNIQUE id associated with the secret
				public int dwLength;	//length of this secret
			}
			#endregion

			#region History
			public enum STATURL_QUERYFLAGS : uint
			{
				STATURL_QUERYFLAG_ISCACHED = 0x00010000,
				STATURL_QUERYFLAG_NOURL = 0x00020000,
				STATURL_QUERYFLAG_NOTITLE = 0x00040000,
				STATURL_QUERYFLAG_TOPLEVEL = 0x00080000,
			}

			public enum STATURLFLAGS : uint
			{
				STATURLFLAG_ISCACHED = 0x00000001,
				STATURLFLAG_ISTOPLEVEL = 0x00000002,
			}

			public enum ADDURL_FLAG : uint
			{
				ADDURL_ADDTOHISTORYANDCACHE = 0,
				ADDURL_ADDTOCACHE = 1
			}

			[ComImport]
			[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			[Guid("3C374A42-BAE4-11CF-BF7D-00AA006946EE")]
			public interface IEnumSTATURL
			{
				void Next(int celt, ref STATURL rgelt, out int pceltFetched);
			}

			[ComImport]
			[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			[Guid("AFA0DC11-C313-11D0-831A-00C04FD5AE38")]
			public interface IUrlHistoryStg
			{
				void AddUrl(string pocsUrl, string pocsTitle, ADDURL_FLAG dwFlags);
				void DeleteUrl(string pocsUrl, int dwFlags);
				void QueryUrl([MarshalAs(UnmanagedType.LPWStr)] string pocsUrl, STATURL_QUERYFLAGS dwFlags, ref STATURL lpSTATURL);
				void BindToObject([In] string pocsUrl, UUID riid, IntPtr ppvOut);
				object EnumUrls
				{
					[return: MarshalAs(UnmanagedType.IUnknown)]
					get;
				}
				void AddUrlAndNotify(string pocsUrl, string pocsTitle, int dwFlags, int fWriteHistory, object poctNotify, object punkISFolder);
			}

			[ComImport]
			[Guid("3C374A40-BAE4-11CF-BF7D-00AA006946EE")]
			public class UrlHistoryClass { }

			public struct UUID
			{
				public int Data1;
				public short Data2;
				public short Data3;
				public byte[] Data4;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct STATURL
			{
				public int cbSize;

				[MarshalAs(UnmanagedType.LPWStr)]
				public string pwcsUrl;

				[MarshalAs(UnmanagedType.LPWStr)]
				public string pwcsTitle;
				
				public long ftLastVisited; // ComTypes.FILETIME
				public long ftLastUpdated; // ComTypes.FILETIME
				public long ftExpires;     // ComTypes.FILETIME
				public STATURLFLAGS dwFlags;
			}
			#endregion
		}
	}
}