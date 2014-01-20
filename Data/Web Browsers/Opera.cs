using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	[DataContract]
	public sealed class Opera : IData<Opera>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public Opera Initiate()
		{
			string NewProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera", "Opera");
			string OldProfilePath = Path.Combine(NewProfilePath, "profile");

			if (Directory.Exists(OldProfilePath))
				this.Profiles = Extensions.ReturnEnumerable(new Profile(new DirectoryInfo(NewProfilePath)).Initiate(), new Profile(new DirectoryInfo(OldProfilePath)).Initiate()).AsSerializable();
			else if (Directory.Exists(NewProfilePath))
				this.Profiles = EnumerableEx.Return(new Profile(new DirectoryInfo(NewProfilePath)).Initiate()).AsSerializable();
			else
				this.Profiles = Enumerable.Empty<Profile>();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Opera"
				: string.Format("Opera ({0})", this.Profiles.Count());
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
			public IEnumerable<History> History { get; private set; }
			[DataMember]
			public IEnumerable<StoredPassword> Passwords { get; private set; }

			public Profile Initiate()
			{
				this.Bookmarks = Extensions.DeferExecutionOnce(Opera.GetBookmarks, this.ProfilePath).AsSerializable();
				this.History = Extensions.DeferExecutionOnce(Opera.GetHistory, this.ProfilePath).AsSerializable();
				this.Passwords = Extensions.DeferExecutionOnce(Opera.GetPasswords, this.ProfilePath).AsSerializable();

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
			public string Title { get; set; }
			[DataMember]
			public Uri Url { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Title)
					? this.Url.ToString()
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
			[Utilities.DisplayConfiguration(Converter = "ExtraFieldsToString")]
			public IDictionary<string, string> ExtraFields { get; set; }
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
			public PasswordType Type { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.Hostname)
					? this.Username
					: string.Format("{0} ({1})", this.Username, this.Hostname);
			}

			private string ExtraFieldsToString(Dictionary<string, string> ExtraFields)
			{
				return string.Join("\r\n", ExtraFields.Select(Field => string.Format("{0} = {1}", Field.Key, Field.Value)));
			}
		}

		public enum PasswordType
		{
			HTTP,
			HTML
		}
		#endregion

		#region Static Methods
		private static IEnumerable<Bookmark> GetBookmarks(DirectoryInfo ProfilePath)
		{
			string BookmarksPath = Path.Combine(ProfilePath.FullName, "bookmarks.adr");

			if (!File.Exists(BookmarksPath))
				return Enumerable.Empty<Bookmark>();

			List<Bookmark> rData = new List<Bookmark>();

			try
			{
				string[] BookmarksData = File.ReadAllLines(BookmarksPath);
				int i = 1;

				while (++i < BookmarksData.Length)
				{
					if (BookmarksData[i] != "#URL")
						continue;

					string Name = null;
					string Url = null;

					while (++i < BookmarksData.Length
						&& BookmarksData[i].Length > 0
						&& BookmarksData[i][0] != '#'
						&& BookmarksData[i].Contains('='))
					{
						string[] Line = BookmarksData[i].TrimStart().Split(new char[] { '=' }, 2);

						if (Line[0] == "NAME")
							Name = Line[1].Trim();
						else if (Line[0] == "URL")
							Url = Line[1].Trim();
					}

					if (string.IsNullOrEmpty(Url))
						continue;

					Uri TempUri;
					rData.Add(new Bookmark
					{
						Title = Name,
						Url = Uri.TryCreate(Url, UriKind.Absolute, out TempUri) ? TempUri : null
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
			string HistoryPath = Path.Combine(ProfilePath.FullName, "global_history.dat");

			if (!File.Exists(HistoryPath))
				return Enumerable.Empty<History>();

			List<History> rData = new List<History>();

			try
			{
				string[] HistoryData = File.ReadAllLines(HistoryPath);

				for (int i = 0; i < HistoryData.Length; i += 4)
				{
					int TempInt;
					Uri TempUri;
					rData.Add(new History
					{
						LastVisit = int.TryParse(HistoryData[i + 2], out TempInt)
							? Opera.UnixEpoch.AddSeconds(TempInt)
							: default(DateTime),
						Title = HistoryData[i],
						Url = Uri.TryCreate(HistoryData[i + 1], UriKind.Absolute, out TempUri) ? TempUri : null
					});
					TempUri = null;
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
			string LoginDataPath = Path.Combine(ProfilePath.FullName, "wand.dat");

			if (!File.Exists(LoginDataPath))
				return Enumerable.Empty<StoredPassword>();

			List<StoredPassword> rData = new List<StoredPassword>();

			try
			{
				List<string> Decrypted = new List<string>();

				using (FileStream WandFile = new FileStream(LoginDataPath, FileMode.Open, FileAccess.Read))
				using (MD5 MD5Hasher = (Environment.OSVersion.Version.Major >= 6) ? (MD5)new MD5Cng() : new MD5CryptoServiceProvider())
				using (TripleDES EncryptionAlgorithm = new TripleDESCryptoServiceProvider()
				{
					Mode = CipherMode.CBC,
					Padding = PaddingMode.None
				})
				{
					while (WandFile.Position < WandFile.Length)
					{
						if (WandFile.ReadByte() != 0x00
							|| WandFile.ReadByte() != 0x00
							|| WandFile.ReadByte() != 0x00
							|| WandFile.ReadByte() != 0x08)
							continue;

						try
						{
							byte[] SectionKey = new byte[8];
							WandFile.Read(SectionKey, 0, SectionKey.Length);

							byte[] blockSize = new byte[sizeof(int)];
							WandFile.Read(blockSize, 0, blockSize.Length);

							int BlockSize = BitConverter.ToInt32(blockSize, 0);
							BlockSize = System.Net.IPAddress.HostToNetworkOrder(BlockSize);

							byte[] EncryptedData = new byte[BlockSize];
							WandFile.Read(EncryptedData, 0, EncryptedData.Length);

							byte[] Temp = new byte[Opera.WandSalt.Length + SectionKey.Length];
							Buffer.BlockCopy(Opera.WandSalt, 0, Temp, 0, Opera.WandSalt.Length);
							Buffer.BlockCopy(SectionKey, 0, Temp, Opera.WandSalt.Length, SectionKey.Length);

							byte[] Hash1 = MD5Hasher.ComputeHash(Temp);

							Temp = new byte[Hash1.Length + Opera.WandSalt.Length + SectionKey.Length];
							Buffer.BlockCopy(Hash1, 0, Temp, 0, Hash1.Length);
							Buffer.BlockCopy(Opera.WandSalt, 0, Temp, Hash1.Length, Opera.WandSalt.Length);
							Buffer.BlockCopy(SectionKey, 0, Temp, Hash1.Length + Opera.WandSalt.Length, SectionKey.Length);

							byte[] Hash2 = MD5Hasher.ComputeHash(Temp);
							Temp = null;

							byte[] EncryptionKey = new byte[24];
							Buffer.BlockCopy(Hash1, 0, EncryptionKey, 0, Hash1.Length);
							Buffer.BlockCopy(Hash2, 0, EncryptionKey, Hash1.Length, 8);

							byte[] EncryptionIV = new byte[8];
							Buffer.BlockCopy(Hash2, 8, EncryptionIV, 0, 8);

							using (ICryptoTransform Decryptor = EncryptionAlgorithm.CreateDecryptor(EncryptionKey, EncryptionIV))
							{
								byte[] Output = Decryptor.TransformFinalBlock(EncryptedData, 0, EncryptedData.Length);
								int LastNull = Output.Length - 1;

								while (LastNull > 0 && Output[LastNull] != 0)
									LastNull--;

								Decrypted.Add(Encoding.Unicode.GetString(Output, 0, LastNull - 1));

								Array.Clear(Output, 0, Output.Length);
								Output = null;
							}

							Array.Clear(Hash1, 0, Hash1.Length);
							Hash1 = null;

							Array.Clear(Hash2, 0, Hash2.Length);
							Hash2 = null;

							Array.Clear(EncryptedData, 0, EncryptedData.Length);
							EncryptedData = null;

							Array.Clear(SectionKey, 0, SectionKey.Length);
							SectionKey = null;
						}
						catch (Exception e)
						{
							Utilities.Utilities.Log(e);
						}
					}
				}

				for (int i = 3; i < Decrypted.Count; i++)
				{
					Uri TempUri;

					if (Decrypted[i].StartsWith("*"))
					{
						rData.Add(new StoredPassword
						{
							Hostname = Uri.TryCreate(Decrypted[i].Substring(1), UriKind.Absolute, out TempUri) ? TempUri.Host : null,
							FormSubmitUrl = TempUri,
							Username = Decrypted[++i],
							Password = Decrypted[++i],
							Type = PasswordType.HTTP
						});
					}
					else
					{
						StoredPassword Password = new StoredPassword
						{
							Hostname = Uri.TryCreate(Decrypted[++i], UriKind.Absolute, out TempUri) ? TempUri.Host : null,
							FormSubmitUrl = TempUri,
							Type = PasswordType.HTML
						};

						Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

						for (i++; i + 1 < Decrypted.Count; i++)
						{
							if (i < Decrypted.Count
								&& (Decrypted[i].StartsWith("*") && Uri.TryCreate(Decrypted[i].Substring(1), UriKind.Absolute, out TempUri))
								|| Uri.TryCreate(Decrypted[i], UriKind.Absolute, out TempUri))
							{
								i--;
								break;
							}

							Fields.Add(Decrypted[i], Decrypted[++i]);
						}

						foreach (string UsernameField in Opera.UsernameFields)
						{
							foreach (KeyValuePair<string, string> Field in Fields)
							{
								if (string.Compare(Field.Key, UsernameField, StringComparison.OrdinalIgnoreCase) == 0)
								{
									Fields.Remove(Field.Key);
									Password.UsernameField = Field.Key;
									Password.Username = Field.Value;
									break;
								}
							}
						}

						if (Password.Username == null)
						{
							KeyValuePair<string, string> UserName = Fields.FirstOrDefault();
							Password.UsernameField = UserName.Key;
							Password.Username = UserName.Value;
							Fields.Remove(UserName.Key);
						}

						foreach (string PasswordField in Opera.PasswordFields)
						{
							foreach (KeyValuePair<string, string> Field in Fields)
							{
								if (string.Compare(Field.Key, PasswordField, StringComparison.OrdinalIgnoreCase) == 0)
								{
									Fields.Remove(Field.Key);
									Password.PasswordField = Field.Key;
									Password.Password = Field.Value;
									break;
								}
							}
						}

						if (Password.Password == null)
						{
							KeyValuePair<string, string> PasswordField = Fields.FirstOrDefault();
							Password.PasswordField = PasswordField.Key;
							Password.Password = PasswordField.Value;
							Fields.Remove(PasswordField.Key);
						}

						Password.ExtraFields = Fields;
						rData.Add(Password);
					}
				}

				Decrypted.Clear();
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
			}

			return rData;
		}
		#endregion

		#region Password Dependancies
		private static readonly string[] UsernameFields = new string[]
		{
			"User",
			"UserName",
			"logon",
			"Email"
		};

		private static readonly string[] PasswordFields = new string[]
		{
			"Password",
			"Pass",
			"Psswrd",
			"Passwd",
			"Psswd",
			"Passwrd"
		};

		private static readonly byte[] WandSalt = new byte[] { 0x83, 0x7D, 0xFC, 0x0F, 0x8E, 0xB3, 0xE8, 0x69, 0x73, 0xAF, 0xFF };
		#endregion

		private static DateTime UnixEpoch = new DateTime(1970, 1, 1);
	}
}