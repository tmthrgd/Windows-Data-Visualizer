using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Com.Xenthrax.WindowsDataVisualizer.Utilities
{
	public sealed class ExceptionEventArgs : EventArgs
	{
		public ExceptionEventArgs(Exception Exception)
		{
			this.Exception = Exception;
		}
		
		public ExceptionEventArgs(string Exception)
		{
			this.Exception = new Exception(Exception);
		}

		public Exception Exception { get; private set; }
		public bool Handled { get; set; }
	}

	public static class Utilities
	{
		#region Log
		#region Log
		public static int NumberOfLoggedExceptions { get; private set; }

		private static TextWriter _LogOut = TextWriter.Null;
		public static TextWriter LogOut
		{
			get
			{
				return Utilities._LogOut;
			}
			set
			{
				if (value == null)
					Utilities._LogOut = null;
				else if (value is MultiWriter)
					Utilities._LogOut = value;
				else
					Utilities._LogOut = TextWriter.Synchronized(value);
			}
		}

		public static event EventHandler<ExceptionEventArgs> ExceptionHandler;

		public static void Log(string Message, Exception innerException)
		{
			Utilities.Log(new Exception(Message, innerException));
		}

		public static void Log(Exception exception)
		{
			if (Utilities.ExceptionHandler != null)
			{
				ExceptionEventArgs e = new ExceptionEventArgs(exception);
				Utilities.ExceptionHandler.Invoke(null, e);

				if (e.Handled)
					return;
			}

			StringBuilder ErrorString = new StringBuilder();

			while (exception != null)
			{
				ErrorString.AppendFormat("{0}\r\n\r\n", exception);
				exception = exception.InnerException;
			}

			Utilities.LogInternal(ErrorString.ToString().TrimEnd('\r', '\n'));
		}

		public static void Log(string Error, params object[] args)
		{
			if (args.Length > 0)
				Error = string.Format(Error, args);

			if (Utilities.ExceptionHandler != null)
			{
				ExceptionEventArgs e = new ExceptionEventArgs(Error);
				Utilities.ExceptionHandler.Invoke(null, e);

				if (e.Handled)
					return;
			}

			Utilities.LogInternal(Error);
		}

		private static void LogInternal(string Error)
		{
			Utilities.NumberOfLoggedExceptions++;

			Error = "========START EXCEPTION========\r\n" +
				Error.Trim() +
				"\r\n=========END EXCEPTION=========\r\n\r\n";

			System.Diagnostics.Debugger.Log(10, "Error", "\r\n" + Error);

			lock (Utilities.LogOut)
			{
				try
				{
					if (Utilities.LogOut.Equals(Console.Out))
					{
						ConsoleColor OriginalColor = Console.ForegroundColor;
						Console.ForegroundColor = ConsoleColor.Red;
						Utilities.LogOut.WriteLine(Error);
						Console.ForegroundColor = OriginalColor;
					}
					else
						Utilities.LogOut.WriteLine(Error);

					Utilities.LogOut.Flush();
				}
				catch (ObjectDisposedException) { }
			}
		}
		#endregion

		#region InitiateLog
		public static void InitiateLog(Stream LogStream)
		{
			LogStream = Stream.Synchronized(LogStream);
			Utilities.InitiateLog(new StreamWriter(LogStream), null);
		}

		public static void InitiateLog(TextWriter TW)
		{
			TW = TextWriter.Synchronized(TW);
			Utilities.InitiateLog(TW, null);
		}

		public static void InitiateLog(Stream LogStream, params TextWriter[] ExtraWriters)
		{
			LogStream = Stream.Synchronized(LogStream);
			Utilities.InitiateLog(new StreamWriter(LogStream), ExtraWriters);
		}

		public static void InitiateLog(TextWriter LogWriter, params TextWriter[] ExtraWriters)
		{
			lock (LogWriter)
			{
				if (LogWriter == null)
					throw new ArgumentNullException("LogWriter");

				if (LogWriter is StreamWriter)
					((StreamWriter)LogWriter).AutoFlush = true;

				TimeSpan UTCOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
				LogWriter.WriteLine("================START LOG: {0} {1}{2:00}{3:00}================\r\n\r\n", DateTime.Now, UTCOffset.Ticks >= 0 ? "+" : string.Empty, UTCOffset.Hours, UTCOffset.Minutes);
				LogWriter.Flush();

				if (Utilities.LogOut != null)
					Utilities.LogOut.Dispose();

				if (ExtraWriters == null || ExtraWriters.Length == 0)
					Utilities.LogOut = LogWriter;
				else
					Utilities.LogOut = new MultiWriter(LogWriter, ExtraWriters);
			}
		}
		#endregion

		public static void CleanUpLog()
		{
			if (Utilities.LogOut == null)
				return;

			lock (Utilities.LogOut)
			{
				TimeSpan _UTCOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);

				if (Utilities.LogOut is MultiWriter)
					((MultiWriter)Utilities.LogOut).BaseWriter.WriteLine("================END LOG: {0} {1}{2:00}{3:00}================\r\n\r\n", DateTime.Now, _UTCOffset.Ticks >= 0 ? "+" : string.Empty, _UTCOffset.Hours, _UTCOffset.Minutes);
				else
					Utilities.LogOut.WriteLine("================END LOG: {0} {1}{2:00}{3:00}================\r\n\r\n", DateTime.Now, _UTCOffset.Ticks >= 0 ? "+" : string.Empty, _UTCOffset.Hours, _UTCOffset.Minutes);
				
				Utilities.LogOut.Flush();
				Utilities.LogOut.Dispose();
				Utilities.LogOut = null;
			}
		}
		#endregion

		#region ToHEX/FromHEX
		public static string ToHEX(byte[] sArray)
		{
			if (sArray == null)
				return null;

			char[] chArray = new char[sArray.Length * 2];
			int index = 0;
			int num3 = 0;

			while (index < sArray.Length)
			{
				int num = (sArray[index] & 240) >> 4;
				chArray[num3++] = HexDigit(num);
				num = sArray[index] & 15;
				chArray[num3++] = HexDigit(num);
				index++;
			}

			return new string(chArray);
		}

		private static char HexDigit(int num)
		{
			return ((num < 10) ? ((char)(num + 0x30)) : ((char)(num + 0x37)));
		}

		public static byte[] FromHEX(string hexString)
		{
			/*
			 * public static byte[] DecodeHexString(string hexString);
			 * 
			 * Declaring Type: System.Security.Util.Hex
			 * Assembly: mscorlib, Version=4.0.0.0
			 */

			byte[] buffer;

			if (hexString == null)
				throw new ArgumentNullException("hexString");

			bool flag = false;
			int num = 0;
			int length = hexString.Length;

			if (((length >= 2) && (hexString[0] == '0')) && ((hexString[1] == 'x') || (hexString[1] == 'X')))
			{
				length = hexString.Length - 2;
				num = 2;
			}

			if (((length % 2) != 0) && ((length % 3) != 2))
				throw new ArgumentException("Invalid Hex Format");//Environment.GetResourceString("Argument_InvalidHexFormat"));

			if ((length >= 3) && (hexString[num + 2] == ' '))
			{
				flag = true;
				buffer = new byte[(length / 3) + 1];
			}
			else
				buffer = new byte[length / 2];

			for (int i = 0; num < hexString.Length; i++)
			{
				int num4 = Utilities.ConvertHexDigit(hexString[num]);
				int num3 = Utilities.ConvertHexDigit(hexString[num + 1]);
				buffer[i] = (byte)(num3 | (num4 << 4));

				if (flag)
					num++;

				num += 2;
			}

			return buffer;
		}

		private static int ConvertHexDigit(char val)
		{
			/*
			 * public static int ConvertHexDigit(char val);
			 * 
			 * Declaring Type: System.Security.Util.Hex
			 * Assembly: mscorlib, Version=4.0.0.0
			 */

			if ((val <= '9') && (val >= '0'))
				return (val - '0');

			if ((val >= 'a') && (val <= 'f'))
				return ((val - 'a') + 10);

			if ((val < 'A') || (val > 'F'))
				throw new ArgumentOutOfRangeException("val");

			return ((val - 'A') + 10);
		}
		#endregion

		#region GetDllType
		public enum DllType : ushort
		{
			I386 = 0x014c,
			IA64 = 0x0200,
			AMD64 = 0x8664
		}

		public static DllType GetDllType(string DllName)
		{
			return Utilities.GetDllType(DllName, null);
		}

		//http://stackoverflow.com/questions/495244/how-can-i-test-a-windows-dll-to-determine-if-it-is-32bit-or-64bit/495305#495305
		public static DllType GetDllType(string DllName, string DllPath)
		{
			IntPtr pImage = IntPtr.Zero;

			try
			{
				pImage = Win32.ImageLoad(DllName, DllPath);
				Win32.LOADED_IMAGE Image = (Win32.LOADED_IMAGE)Marshal.PtrToStructure(pImage, typeof(Win32.LOADED_IMAGE));
				Win32.IMAGE_NT_HEADERS Header = (Win32.IMAGE_NT_HEADERS)Marshal.PtrToStructure(Image.FileHeader, typeof(Win32.IMAGE_NT_HEADERS));
				return (DllType)Header.FileHeader.Machine;
			}
			finally
			{
				if (pImage != IntPtr.Zero)
					Win32.ImageUnload(pImage);
			}
		}
		#endregion

		#region IsDllArchMismatch
		public static bool IsDllArchMismatch(string DllName, bool Targetx64)
		{
			return Utilities.IsDllArchMismatch(DllName, null, Targetx64);
		}

		public static bool IsDllArchMismatch(string DllName, string DllPath, bool Targetx64)
		{
			Utilities.DllType Type = Utilities.GetDllType(DllName, DllPath);
			return (Type == DllType.I386 && Targetx64)
				|| ((Type == DllType.AMD64 || Type == DllType.IA64) && !Targetx64);
		}
		#endregion

		#region GetMimeType
		private static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

		//http://kseesharp.blogspot.com/2008/04/c-get-mimetype-from-file-name.html
		public static string GetMimeType(string FileName)
		{
			string Extension = Path.GetExtension(FileName);

			if (!MimeTypes.ContainsKey(Extension))
			{
				using (RegistryKey rk = Registry.ClassesRoot.OpenSubKey(Extension.ToLower(), false))
				{
					MimeTypes.Add(Extension, (rk != null && rk.GetValue("Content Type") != null)
						? rk.GetValue("Content Type").ToString()
						: "application/unknown");
				}
			}

			return MimeTypes[Extension];
		}
		#endregion

		public static string FromCamelCase(string value)
		{
			// Revert CamelCase, AbcDef becomes Abc Def
			var matches = System.Text.RegularExpressions.Regex
				.Matches(value, "[A-Z][a-z]+");

			if (matches.Count <= 0)
				return value;

			return matches
				.OfType<System.Text.RegularExpressions.Match>()
				.Select(match => match.Value)
				.Aggregate((a, b) => string.Format("{0} {1}", a, b))
				.TrimStart(' ');
		}
	}
}