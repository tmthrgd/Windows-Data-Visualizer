using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Com.Xenthrax.WindowsDataVisualizer.Data
{
	using Com.Xenthrax.DllInjector;

	[DataContract]
	public sealed class Windows : IData<Windows>
	{
		[DataMember]
		[Utilities.DisplayConfiguration(Converter = "ProfilesConverter")]
		public IEnumerable<Profile> Profiles { get; private set; }

		public Windows Initiate()
		{
			this.Profiles = EnumerableEx.Return(new Profile().Initiate()).AsSerializable();

			return this;
		}

		public override string ToString()
		{
			return (this.Profiles == null)
				? "Windows"
				: string.Format("Windows ({0})", this.Profiles.Count());
		}

		private string ProfilesConverter(IEnumerable<Profile> Data)
		{
			return string.Format("{0}\r\n\t{1}", Data.Count(), string.Join("\r\n\t", Data)).TrimEnd('\r', '\n', '\t');
		}

		#region Types
		public sealed class Profile : IData<Profile>
		{
			[DataMember]
			public IEnumerable<PasswordHash> PasswordHashes { get; private set; }
			[DataMember]
			public IEnumerable<ProductKey> ProductKeys { get; private set; }

			public Profile Initiate()
			{
				this.PasswordHashes = Extensions.DeferExecutionOnce(Windows.GetPasswordHashes).AsSerializable();
				this.ProductKeys = Extensions.DeferExecutionOnce(Windows.GetProductKeys).AsSerializable();

				return this;
			}

			public override string ToString()
			{
				return Environment.MachineName;
			}
		}

		public sealed class PasswordHash : IData
		{
			[DataMember]
			[Utilities.DisplayConfiguration(Ignore = true)]
			public string DomainName { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration(Converter = "UserNameConverter")]
			public string UserName { get; set; }
			[DataMember]
			public string FullName { get; set; }
			[DataMember]
			public string Description { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration("RID (Relative ID)")]
			public uint RID { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration("SID (Security Identifier)")]
			public string SID { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration("LM Hash", Converter = "LMHashConverter")]
			public string LMHash { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration("NT Hash", Converter = "NTHashConverter")]
			public string NTHash { get; set; }

			public override string ToString()
			{
				return string.IsNullOrEmpty(this.DomainName)
					? this.UserName
					: string.Format("{0}\\{1}", this.DomainName, this.UserName);
			}

			private string UserNameConverter(string UserName)
			{
				return string.IsNullOrEmpty(this.DomainName)
					? UserName
					: string.Format("{0}\\{1}", this.DomainName, UserName);
			}

			private string LMHashConverter(string LMHash)
			{
				if (LMHash == "AAD3B435B51404EEAAD3B435B51404EE")
					LMHash += " (Empty password hash)";

				return LMHash;
			}

			private string NTHashConverter(string NTHash)
			{
				if (NTHash == "31D6CFE0D16AE931B73C59D7E0C089C0")
					NTHash += " (Empty password hash)";

				return NTHash;
			}
		}

		public sealed class ProductKey : IData
		{
			[DataMember]
			public string Program { get; set; }
			[DataMember]
			[Utilities.DisplayConfiguration("Product Key")]
			public string Value { get; set; }

			public override string ToString()
			{
				return this.Program;
			}
		}
		#endregion

		#region Static Methods
		private static IEnumerable<PasswordHash> GetPasswordHashes()
		{
			try
			{
				using (DllInjector Injector = new DllInjector("winlogon.exe", true))
				{
					Injector.AcquireProcessHandle();

					RegCloseKeyPrototype RegCloseKey = Injector.GetFunction<RegCloseKeyPrototype>(true);
					RegEnumKeyExPrototype RegEnumKeyEx = Injector.GetFunction<RegEnumKeyExPrototype>();
					RegGetValuePrototype RegGetValue = Injector.GetFunction<RegGetValuePrototype>();
					RegOpenKeyExPrototype RegOpenKeyEx = Injector.GetFunction<RegOpenKeyExPrototype>();
					RegOpenKeyPrototype RegOpenKey = Injector.GetFunction<RegOpenKeyPrototype>();
					RegQueryInfoKeyPrototype RegQueryInfoKey = Injector.GetFunction<RegQueryInfoKeyPrototype>();

					byte[] BootKey = GetBootKey(RegCloseKey, RegOpenKeyEx, RegOpenKey, RegQueryInfoKey);
					byte[] SamAccountF = GetSamAccountF(RegCloseKey, RegGetValue, RegOpenKeyEx);
					RegistryUser[] Users = GetUsers(RegCloseKey, RegEnumKeyEx, RegGetValue, RegOpenKeyEx, RegQueryInfoKey);

					List<PasswordHash> rData = new List<PasswordHash>(Users.Length);

					using (MD5 MD5Hasher = (Environment.OSVersion.Version.Major >= 6) ? (MD5)new MD5Cng() : new MD5CryptoServiceProvider())
					using (DES EncryptionAlgorithm = new DESCryptoServiceProvider()
					{
						Mode = CipherMode.ECB,
						Padding = PaddingMode.None
					})
					{
						byte[] BootKeyRC4Key = new byte[0x10 + aqwerty.Length + BootKey.Length + anum.Length];
						Buffer.BlockCopy(SamAccountF, 0x70, BootKeyRC4Key, 0, 0x10);
						Buffer.BlockCopy(aqwerty, 0, BootKeyRC4Key, 0x10, aqwerty.Length);
						Buffer.BlockCopy(BootKey, 0, BootKeyRC4Key, 0x10 + aqwerty.Length, BootKey.Length);
						Buffer.BlockCopy(anum, 0, BootKeyRC4Key, 0x10 + aqwerty.Length + BootKey.Length, anum.Length);

						BootKeyRC4Key = MD5Hasher.ComputeHash(BootKeyRC4Key);

						byte[] HashedBootKey = new byte[0x20];
						Buffer.BlockCopy(SamAccountF, 0x80, HashedBootKey, 0, HashedBootKey.Length);

						RC4(HashedBootKey, BootKeyRC4Key);

						foreach (RegistryUser User in Users)
						{
							int UserNameOffset = BitConverter.ToInt32(User.V, 0x0C) + 0xCC;
							int UserNameLength = BitConverter.ToInt32(User.V, 0x10);
							string UserName = Encoding.Unicode.GetString(User.V, UserNameOffset, UserNameLength);

							int FullNameOffset = BitConverter.ToInt32(User.V, 0x18) + 0xCC;
							int FullNameLength = BitConverter.ToInt32(User.V, 0x1C);
							string FullName = Encoding.Unicode.GetString(User.V, FullNameOffset, FullNameLength);

							int DescriptionOffset = BitConverter.ToInt32(User.V, 0x24) + 0xCC;
							int DescriptionLength = BitConverter.ToInt32(User.V, 0x28);
							string Description = Encoding.Unicode.GetString(User.V, DescriptionOffset, DescriptionLength);

							int LMHashOffset = BitConverter.ToInt32(User.V, 0x9C) + 0xCC + 4;
							int LMHashLength = BitConverter.ToInt32(User.V, 0xA0) - 4;

							int NTHashOffset = BitConverter.ToInt32(User.V, 0xA8) + 0xCC + 4;
							int NTHashLength = BitConverter.ToInt32(User.V, 0xAC) - 4;

							byte[] LMHash = (LMHashLength == 16) ? new byte[LMHashLength] : null;
							byte[] NTHash = (NTHashLength == 16) ? new byte[NTHashLength] : null;

							if (LMHash != null || NTHash != null)
							{
								byte[] EncLMHash = (LMHash != null) ? new byte[LMHashLength] : null;
								byte[] EncNTHash = (NTHash != null) ? new byte[NTHashLength] : null;

								byte[] s1 = new byte[8]
								{
									(byte)((User.RID & 0xFF) >> 1),
									(byte)(((User.RID & 0x01) << 6) | (((User.RID >> 8) & 0xFF) >> 2)),
									(byte)((((User.RID >> 8) & 0x03) << 5) | (((User.RID >> 16) & 0xFF) >> 3)),
									(byte)((((User.RID >> 16) & 0x07) << 4) | (((User.RID >> 24) & 0xFF) >> 4)),
									(byte)((((User.RID >> 24) & 0x0F) << 3) | ((User.RID & 0xFF) >> 5)),
									(byte)(((User.RID & 0x1F) << 2) | (((User.RID >> 8) & 0xFF) >> 6)),
									(byte)((((User.RID >> 8) & 0x3F) << 1) | (((User.RID >> 16) & 0xFF) >> 7)),
									(byte)((User.RID >> 16) & 0x7F)
								};

								byte[] s2 = new byte[8]
								{
									(byte)(((User.RID >> 24) & 0xFF) >> 1),
									(byte)((((User.RID >> 24) & 0x01) << 6) | ((User.RID & 0xFF) >> 2)),
									(byte)(((User.RID & 0x03) << 5) | (((User.RID >> 8) & 0xFF) >> 3)),
									(byte)(((((User.RID >> 8) & 0x07) << 4) | ((User.RID >> 16) & 0xFF) >> 4)),
									(byte)((((User.RID >> 16) & 0x0F) << 3) | (((User.RID >> 24) & 0xFF) >> 5)),
									(byte)((((User.RID >> 24) & 0x1F) << 2) | ((User.RID & 0xFF) >> 6)),
									(byte)(((User.RID & 0x3F) << 1) | (((User.RID >> 8) & 0xFF) >> 7)),
									(byte)((User.RID >> 8) & 0x7F)
								};

								for (int i = 0; i < s1.Length; i++)
									s1[i] = odd_parity[s1[i] << 1];

								for (int i = 0; i < s2.Length; i++)
									s2[i] = odd_parity[s2[i] << 1];

								byte[] RID = BitConverter.GetBytes(User.RID);

								if (LMHash != null)
								{
									Buffer.BlockCopy(User.V, LMHashOffset, EncLMHash, 0, LMHashLength);

									byte[] LMHashKey = new byte[0x10 + RID.Length + LMPassword.Length];
									Buffer.BlockCopy(HashedBootKey, 0, LMHashKey, 0, 0x10);
									Buffer.BlockCopy(RID, 0, LMHashKey, 0x10, RID.Length);
									Buffer.BlockCopy(LMPassword, 0, LMHashKey, 0x10 + RID.Length, LMPassword.Length);

									LMHashKey = MD5Hasher.ComputeHash(LMHashKey);
									RC4(EncLMHash, LMHashKey);
								}

								if (NTHash != null)
								{
									Buffer.BlockCopy(User.V, NTHashOffset, EncNTHash, 0, NTHashLength);

									byte[] NTHashKey = new byte[0x10 + RID.Length + NTPassword.Length];
									Buffer.BlockCopy(HashedBootKey, 0, NTHashKey, 0, 0x10);
									Buffer.BlockCopy(RID, 0, NTHashKey, 0x10, RID.Length);
									Buffer.BlockCopy(NTPassword, 0, NTHashKey, 0x10 + RID.Length, NTPassword.Length);

									NTHashKey = MD5Hasher.ComputeHash(NTHashKey);
									RC4(EncNTHash, NTHashKey);
								}

								using (ICryptoTransform Decryptor = EncryptionAlgorithm.CreateDecryptor(s1, null))
								{
									if (LMHash != null)
									{
										byte[] LMHash2 = Decryptor.TransformFinalBlock(EncLMHash, 0, EncLMHash.Length / 2);
										Buffer.BlockCopy(LMHash2, 0, LMHash, 0, LMHash.Length / 2);
									}

									if (NTHash != null)
									{
										byte[] NTHash2 = Decryptor.TransformFinalBlock(EncNTHash, 0, EncNTHash.Length / 2);
										Buffer.BlockCopy(NTHash2, 0, NTHash, 0, NTHash.Length / 2);
									}
								}

								using (ICryptoTransform Decryptor = EncryptionAlgorithm.CreateDecryptor(s2, null))
								{
									if (LMHash != null)
									{
										byte[] LMHash2 = Decryptor.TransformFinalBlock(EncLMHash, EncLMHash.Length / 2, EncLMHash.Length / 2);
										Buffer.BlockCopy(LMHash2, 0, LMHash, LMHash.Length / 2, LMHash.Length / 2);
									}

									if (NTHash != null)
									{
										byte[] NTHash2 = Decryptor.TransformFinalBlock(EncNTHash, EncNTHash.Length / 2, EncNTHash.Length / 2);
										Buffer.BlockCopy(NTHash2, 0, NTHash, NTHash.Length / 2, NTHash.Length / 2);
									}
								}
							}

							uint cbSid = 0;
							uint cchReferencedDomainName = 0;
							SID_NAME_USE peUse;

							if (LookupAccountName(null, UserName, null, ref cbSid, null, ref cchReferencedDomainName, out peUse))
								throw new Exception("LookupAccountName failed, Username not found");

							byte[] Sid = new byte[cbSid];
							StringBuilder DomainName = new StringBuilder((int)cchReferencedDomainName);

							if (!LookupAccountName(null, UserName, Sid, ref cbSid, DomainName, ref cchReferencedDomainName, out peUse))
								throw new Win32Exception(Marshal.GetLastWin32Error());

							string SID;

							if (!ConvertSidToStringSid(Sid, out SID))
								throw new Win32Exception(Marshal.GetLastWin32Error());

							rData.Add(new PasswordHash
							{
								Description = Description,
								DomainName = DomainName.ToString(),
								FullName = FullName,
								LMHash = (LMHash == null)
									? "AAD3B435B51404EEAAD3B435B51404EE"
									: new SoapHexBinary(LMHash).ToString(),
								NTHash = (NTHash == null)
									? "31D6CFE0D16AE931B73C59D7E0C089C0"
									: new SoapHexBinary(NTHash).ToString(),
								RID = User.RID,
								SID = SID,
								UserName = UserName
							});
						}
					}

					return rData;
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<PasswordHash>();
			}
		}
		
		private static IEnumerable<ProductKey> GetProductKeys()
		{
			char[] Digits = new char[]
			{
				'B', 'C', 'D', 'F', 'G',
				'H', 'J', 'K', 'M', 'P',
				'Q', 'R', 'T', 'V', 'W',
				'X', 'Y', '2', '3', '4',
				'6', '7', '8', '9'
			};

			try
			{
				using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(ProductKeyReg, false))
				{
					if (rk == null)
						return Enumerable.Empty<ProductKey>();

					byte[] DigitalProductId = rk.GetValue("DigitalProductId") as byte[];

					if (DigitalProductId == null)
						return Enumerable.Empty<ProductKey>();

					byte[] HexPid = new byte[15];
					char[] ProductKey = new char[29];
					Buffer.BlockCopy(DigitalProductId, 52, HexPid, 0, HexPid.Length);

					for (int i = 28; i >= 0; i--)
					{
						if ((i + 1) % 6 == 0)
							ProductKey[i] = '-';
						else
						{
							int DigitMapIndex = 0;

							for (int j = 14; j >= 0; j--)
							{
								int byteValue = (DigitMapIndex << 8) | HexPid[j];
								HexPid[j] = (byte)(byteValue / 24);
								DigitMapIndex = byteValue % 24;
								ProductKey[i] = Digits[DigitMapIndex];
							}
						}
					}

					string ProductName = rk.GetValue("ConvertToEdition") as string;
					ProductName = string.IsNullOrWhiteSpace(ProductName)
						? rk.GetValue("ProductName") as string
						: ProductName;

					return EnumerableEx.Return(new ProductKey
					{
						Value = new string(ProductKey),
						Program = string.IsNullOrWhiteSpace(ProductName)
							? Environment.OSVersion.ToString()
							: ProductName
					});
				}
			}
			catch (Exception e)
			{
				Utilities.Utilities.Log(e);
				return Enumerable.Empty<ProductKey>();
			}
		}

		#region GetPasswordHashes Dependancies
		private static byte[] GetBootKey(RegCloseKeyPrototype RegCloseKey, RegOpenKeyExPrototype RegOpenKeyEx, RegOpenKeyPrototype RegOpenKey, RegQueryInfoKeyPrototype RegQueryInfoKey)
		{
			IntPtr hKey;
			IntPtr JDsk = IntPtr.Zero;
			IntPtr Skew1sk = IntPtr.Zero;
			IntPtr GBGsk = IntPtr.Zero;
			IntPtr Datask = IntPtr.Zero;

			string JD = null;
			string Skew1 = null;
			string GBG = null;
			string Data = null;

			uint Status = RegOpenKeyEx(HKEY_LOCAL_MACHINE, "SYSTEM\\CurrentControlSet\\Control\\Lsa", 0, KEY_READ, out hKey);

			try
			{
				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				Status = RegOpenKey(hKey, "JD", out JDsk);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				uint dummy;
				uint cbBootKey = 9;
				StringBuilder pbBootKey = new StringBuilder(9);
				Status = RegQueryInfoKey(JDsk, pbBootKey, ref cbBootKey, IntPtr.Zero, out dummy, out dummy, out dummy, out dummy, out dummy, out dummy, out dummy, IntPtr.Zero);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				JD = pbBootKey.ToString(0, (int)cbBootKey);
				Status = RegOpenKey(hKey, "Skew1", out Skew1sk);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				cbBootKey = 9;
				Status = RegQueryInfoKey(Skew1sk, pbBootKey, ref cbBootKey, IntPtr.Zero, out dummy, out dummy, out dummy, out dummy, out dummy, out dummy, out dummy, IntPtr.Zero);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				Skew1 = pbBootKey.ToString(0, (int)cbBootKey);
				Status = RegOpenKey(hKey, "GBG", out GBGsk);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				cbBootKey = 9;
				Status = RegQueryInfoKey(GBGsk, pbBootKey, ref cbBootKey, IntPtr.Zero, out dummy, out dummy, out dummy, out dummy, out dummy, out dummy, out dummy, IntPtr.Zero);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				GBG = pbBootKey.ToString(0, (int)cbBootKey);
				Status = RegOpenKey(hKey, "Data", out Datask);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				cbBootKey = 9;
				Status = RegQueryInfoKey(Datask, pbBootKey, ref cbBootKey, IntPtr.Zero, out dummy, out dummy, out dummy, out dummy, out dummy, out dummy, out dummy, IntPtr.Zero);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				Data = pbBootKey.ToString(0, (int)cbBootKey);
			}
			finally
			{
				if (Datask != IntPtr.Zero)
					RegCloseKey(Datask);

				if (GBGsk != IntPtr.Zero)
					RegCloseKey(GBGsk);

				if (Skew1sk != IntPtr.Zero)
					RegCloseKey(Skew1sk);

				if (JDsk != IntPtr.Zero)
					RegCloseKey(JDsk);

				if (hKey != IntPtr.Zero)
					RegCloseKey(hKey);

				if (hKey != IntPtr.Zero)
					RegCloseKey(hKey);
			}

			byte[] _BootKey = SoapHexBinary.Parse(JD + Skew1 + GBG + Data).Value;
			byte[] BootKey = new byte[_BootKey.Length];

			for (int i = 0; i < _BootKey.Length; i++)
				BootKey[i] = _BootKey[BootKeyMatrix[i]];

			return BootKey;
		}

		private static byte[] GetSamAccountF(RegCloseKeyPrototype RegCloseKey, RegGetValuePrototype RegGetValue, RegOpenKeyExPrototype RegOpenKeyEx)
		{
			IntPtr hKey;
			uint Status = RegOpenKeyEx(HKEY_LOCAL_MACHINE, "SAM\\SAM\\Domains\\Account", 0, KEY_READ, out hKey);

			try
			{
				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				uint dummy;
				uint cbValue = 0;
				Status = RegGetValue(hKey, null, "F", RRF_RT_ANY, out dummy, null, ref cbValue);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				byte[] pbValue = new byte[cbValue];
				Status = RegGetValue(hKey, null, "F", RRF_RT_ANY, out dummy, pbValue, ref cbValue);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				return pbValue;
			}
			finally
			{
				if (hKey != IntPtr.Zero)
					RegCloseKey(hKey);
			}
		}

		private static RegistryUser[] GetUsers(RegCloseKeyPrototype RegCloseKey, RegEnumKeyExPrototype RegEnumKeyEx, RegGetValuePrototype RegGetValue, RegOpenKeyExPrototype RegOpenKeyEx, RegQueryInfoKeyPrototype RegQueryInfoKey)
		{
			IntPtr hKey;
			uint Status = RegOpenKeyEx(HKEY_LOCAL_MACHINE, "SAM\\SAM\\Domains\\Account\\Users", 0, KEY_READ, out hKey);

			try
			{
				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				uint dummy = 0;
				uint lpcSubKeys;
				uint lpcMaxSubKeyLen;
				Status = RegQueryInfoKey(hKey, null, ref dummy, IntPtr.Zero, out lpcSubKeys, out lpcMaxSubKeyLen, out dummy, out dummy, out dummy, out dummy, out dummy, IntPtr.Zero);

				if (Status != ERROR_SUCCESS)
					throw new Win32Exception((int)Status);

				List<RegistryUser> Users = new List<RegistryUser>((int)lpcSubKeys);
				StringBuilder lpName = new StringBuilder((int)lpcMaxSubKeyLen + 1);

				for (uint i = 0; i < lpcSubKeys; i++)
				{
					uint lpcName = lpcMaxSubKeyLen + 1;
					Status = RegEnumKeyEx(hKey, i, lpName, ref lpcName, IntPtr.Zero, null, ref dummy, IntPtr.Zero);

					if (Status != ERROR_SUCCESS)
						throw new Win32Exception((int)Status);

					if (lpcName != 8)
						continue;

					string Name = lpName.ToString(0, (int)lpcName);

					uint cbValueSize = 0;
					Status = RegGetValue(hKey, Name, "V", RRF_RT_ANY, out dummy, null, ref cbValueSize);

					if (Status != ERROR_SUCCESS)
						throw new Win32Exception((int)Status);

					byte[] pbValue = new byte[cbValueSize];
					Status = RegGetValue(hKey, Name, "V", RRF_RT_ANY, out dummy, pbValue, ref cbValueSize);

					if (Status != ERROR_SUCCESS)
						throw new Win32Exception((int)Status);

					Users.Add(new RegistryUser
					{
						V = pbValue,
						RID = Convert.ToUInt32(Name, 16)
					});
				}

				return Users.ToArray();
			}
			finally
			{
				if (hKey != IntPtr.Zero)
					RegCloseKey(hKey);
			}
		}

		private static void RC4(byte[] bytes, byte[] key)
		{
			byte[] s = new byte[256];
			byte[] k = new byte[256];

			for (int i = 0; i < 256; i++)
			{
				s[i] = (byte)i;
				k[i] = key[i % key.Length];
			}

			for (int i = 0, j = 0; i < 256; i++)
			{
				j = (j + s[i] + k[i]) % 256;
				byte temp = s[i];
				s[i] = s[j];
				s[j] = temp;
			}

			for (int x = 0, i = 0, j = 0; x < bytes.Length; x++)
			{
				i = (i + 1) % 256;
				j = (j + s[i]) % 256;
				byte temp = s[i];
				s[i] = s[j];
				s[j] = temp;
				int t = (s[i] + s[j]) % 256;
				bytes[x] ^= s[t];
			}
		}
		#endregion
		#endregion

		private const string ProductKeyReg = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

		#region GetPasswordHashes Dependancies
		private const uint ERROR_SUCCESS = 0x0;
		private const uint ERROR_INSUFFICIENT_BUFFER = 0x7A;
		private const uint ERROR_INVALID_FLAGS = 0x3EC;

		private const uint KEY_READ = (0x00020000 | 0x0001 | 0x0008 | 0x0010) & ~0x00100000;

		private const uint RRF_RT_ANY = 0x0000ffff;

		private static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));

		[RemoteImport("Advapi32.dll", "RegCloseKey", CallingConvention = CallingConvention.StdCall)]
		private delegate uint RegCloseKeyPrototype(IntPtr hKey);

		[RemoteImport("Advapi32.dll", "RegEnumKeyExW", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		private delegate uint RegEnumKeyExPrototype(IntPtr hKey, uint dwIndex, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpName, ref uint lpcName, IntPtr lpReserved, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpClass, ref uint lpcClass, IntPtr lpftLastWriteTime);

		[RemoteImport("Advapi32.dll", "RegGetValueW", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		private delegate uint RegGetValuePrototype(IntPtr hkey, [MarshalAs(UnmanagedType.LPTStr)] string lpSubKey, [MarshalAs(UnmanagedType.LPTStr)] string lpValue, uint dwFlags, out uint pdwType, [MarshalAs(UnmanagedType.LPArray)] byte[] pvData, ref uint pcbData);

		[RemoteImport("Advapi32.dll", "RegOpenKeyExW", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		private delegate uint RegOpenKeyExPrototype(IntPtr hKey, [MarshalAs(UnmanagedType.LPTStr)] string SubKey, uint ulOptions, uint samDesired, out IntPtr hkResult);

		[RemoteImport("Advapi32.dll", "RegOpenKeyW", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		private delegate uint RegOpenKeyPrototype(IntPtr hKey, [MarshalAs(UnmanagedType.LPTStr)] string SubKey, out IntPtr hkResult);

		[RemoteImport("Advapi32.dll", "RegQueryInfoKeyW", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		private delegate uint RegQueryInfoKeyPrototype(IntPtr hKey, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpClass, ref uint lpcClass, IntPtr lpReserved, out uint lpcSubKeys, out uint lpcMaxSubKeyLen, out uint lpcMaxClassLen, out uint lpcValues, out uint lpcMaxValueNameLen, out uint lpcMaxValueLen, out uint lpcbSecurityDescriptor, IntPtr lpftLastWriteTime);

		[DllImport("Advapi32.dll", EntryPoint = "LookupAccountNameW", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool LookupAccountName([MarshalAs(UnmanagedType.LPTStr)] string lpSystemName, [MarshalAs(UnmanagedType.LPTStr)] string lpAccountName, [MarshalAs(UnmanagedType.LPArray)] byte[] Sid, ref uint cbSid, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder ReferencedDomainName, ref uint cchReferencedDomainName, out SID_NAME_USE peUse);

		[DllImport("Advapi32.dll", EntryPoint = "ConvertSidToStringSidW", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool ConvertSidToStringSid([MarshalAs(UnmanagedType.LPArray)] byte[] Sid, [MarshalAs(UnmanagedType.LPTStr)] out string StringSid);

		private enum SID_NAME_USE : uint
		{
			SidTypeUser = 1,
			SidTypeGroup,
			SidTypeDomain,
			SidTypeAlias,
			SidTypeWellKnownGroup,
			SidTypeDeletedAccount,
			SidTypeInvalid,
			SidTypeUnknown,
			SidTypeComputer
		}

		private static readonly byte[] BootKeyMatrix = new byte[] { 0x8, 0x5, 0x4, 0x2, 0xb, 0x9, 0xd, 0x3, 0x0, 0x6, 0x1, 0xc, 0xe, 0xa, 0xf, 0x7 };

		private static byte[] odd_parity = new byte[]
		{
			  1,   1,   2,   2,   4,   4,   7,   7,   8,   8,  11,  11,  13,  13,  14,  14,
			 16,  16,  19,  19,  21,  21,  22,  22,  25,  25,  26,  26,  28,  28,  31,  31,
			 32,  32,  35,  35,  37,  37,  38,  38,  41,  41,  42,  42,  44,  44,  47,  47,
			 49,  49,  50,  50,  52,  52,  55,  55,  56,  56,  59,  59,  61,  61,  62,  62,
			 64,  64,  67,  67,  69,  69,  70,  70,  73,  73,  74,  74,  76,  76,  79,  79,
			 81,  81,  82,  82,  84,  84,  87,  87,  88,  88,  91,  91,  93,  93,  94,  94,
			 97,  97,  98,  98, 100, 100, 103, 103, 104, 104, 107, 107, 109, 109, 110, 110,
			112, 112, 115, 115, 117, 117, 118, 118, 121, 121, 122, 122, 124, 124, 127, 127,
			128, 128, 131, 131, 133, 133, 134, 134, 137, 137, 138, 138, 140, 140, 143, 143,
			145, 145, 146, 146, 148, 148, 151, 151, 152, 152, 155, 155, 157, 157, 158, 158,
			161, 161, 162, 162, 164, 164, 167, 167, 168, 168, 171, 171, 173, 173, 174, 174,
			176, 176, 179, 179, 181, 181, 182, 182, 185, 185, 186, 186, 188, 188, 191, 191,
			193, 193, 194, 194, 196, 196, 199, 199, 200, 200, 203, 203, 205, 205, 206, 206,
			208, 208, 211, 211, 213, 213, 214, 214, 217, 217, 218, 218, 220, 220, 223, 223,
			224, 224, 227, 227, 229, 229, 230, 230, 233, 233, 234, 234, 236, 236, 239, 239,
			241, 241, 242, 242, 244, 244, 247, 247, 248, 248, 251, 251, 253, 253, 254, 254
		};

		private static byte[] aqwerty = Encoding.ASCII.GetBytes("!@#$%^&*()qwertyUIOPAzxcvbnmQQQQQQQQQQQQ)(*@&%\0");
		private static byte[] anum = Encoding.ASCII.GetBytes("0123456789012345678901234567890123456789\0");
		private static byte[] LMPassword = Encoding.ASCII.GetBytes("LMPASSWORD\0");
		private static byte[] NTPassword = Encoding.ASCII.GetBytes("NTPASSWORD\0");

		private class RegistryUser
		{
			public byte[] V;
			public uint RID;
		}
		#endregion
	}
}