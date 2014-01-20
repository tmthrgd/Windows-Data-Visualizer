using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FE3458D878534D9183D79D9318BB08C0.Utilities
{
	internal class DllInjector : IDisposable
	{
		#region Constructors
		public DllInjector(string DllPath, string ProcessName)
			: this(DllPath, ProcessName, true)
		{
		}

		public DllInjector(string DllPath, string ProcessName, bool EjectOnDispose)
		{
			if (string.IsNullOrEmpty(DllPath))
				throw new ArgumentNullException("DllPath");

			if (string.IsNullOrEmpty(ProcessName))
				throw new ArgumentNullException("ProcessName");

			this.DllPath = Path.GetFullPath(DllPath);
			this.EjectOnDispose = EjectOnDispose;
			this.TargetProcess = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ProcessName)).FirstOrDefault();
		}

		public DllInjector(string DllPath, Process TargetProcesss)
			: this(DllPath, TargetProcesss, true)
		{
		}

		public DllInjector(string DllPath, Process TargetProcesss, bool EjectOnDispose)
		{
			if (string.IsNullOrEmpty(DllPath))
				throw new ArgumentNullException("DllPath");

			if (TargetProcesss == null)
				throw new ArgumentNullException("TargetProcesss");

			this.DllPath = Path.GetFullPath(DllPath);
			this.EjectOnDispose = EjectOnDispose;
			this.TargetProcess = TargetProcesss;
		}
		#endregion

		#region Members
		private static int DebugModeReferences = 0;

		private bool Disposed = false;

		private string _DllPath;
		private bool _EjectOnDispose;
		private bool _IsInjected;
		private Process _TargetProcess;

		private IntPtr hProc;
		private IntPtr hLibModule;
		private IntPtr hDll;
		#endregion

		#region Properties
		public string DllPath
		{
			get
			{
				if (this.Disposed)
					throw new ObjectDisposedException(this.GetType().Name);

				return this._DllPath;
			}
			private set
			{
				this._DllPath = value;
			}
		}

		public bool EjectOnDispose
		{
			get
			{
				if (this.Disposed)
					throw new ObjectDisposedException(this.GetType().Name);

				return this._EjectOnDispose;
			}
			private set
			{
				this._EjectOnDispose = value;
			}
		}

		public bool IsInjected
		{
			get
			{
				if (this.Disposed)
					throw new ObjectDisposedException(this.GetType().Name);

				return this._IsInjected;
			}
			private set
			{
				this._IsInjected = value;
			}
		}

		public Process TargetProcess
		{
			get
			{
				if (this.Disposed)
					throw new ObjectDisposedException(this.GetType().Name);

				return this._TargetProcess;
			}
			set
			{
				if (this.IsInjected)
				{
					this.Eject();
					this._TargetProcess = value;
					this.Inject();
				}
				else
					this._TargetProcess = value;
			}
		}
		#endregion

		#region Methods
		private IntPtr CreateRemoteThread(IntPtr hFunc, IntPtr hRemoteMem)
		{
			if (hFunc == IntPtr.Zero)
				throw new ArgumentNullException("hFunc");

			if (this.hProc == IntPtr.Zero)
				throw new NullReferenceException("this.hProc");

			IntPtr hRemoteThread = IntPtr.Zero;

			if (Utilities.DoesWin32MethodExist("Ntdll.dll", "NtCreateThreadEx"))
			{
				uint Status = DllInjector.NtCreateThreadEx(out hRemoteThread, 0x1FFFFF, IntPtr.Zero, this.hProc, hFunc, hRemoteMem, false, 0, 0, 0, IntPtr.Zero);

				//if (Status != 0x0)
				if (Status != 0x0 || hRemoteThread == IntPtr.Zero)
				{
					if (hRemoteThread == IntPtr.Zero)
						hRemoteThread = Win32.CreateRemoteThread(this.hProc, IntPtr.Zero, 0, hFunc, hRemoteMem, Win32.CreateRemoteThreadCreationFlags.None, IntPtr.Zero);

					if (hRemoteThread != IntPtr.Zero)
						Utilities.Log("DllInjector.CreateRemoteThread, NtCreateThreadEx failed, 0x{0}\r\n(See http://msdn.microsoft.com/en-us/library/cc704588.aspx)", Convert.ToString(Status, 16).ToUpper());
					else
						throw new Exception(string.Format("DllInjector.CreateRemoteThread, NtCreateThreadEx failed, 0x{0}\r\n(See http://msdn.microsoft.com/en-us/library/cc704588.aspx)", Convert.ToString(Status, 16).ToUpper()));
				}
			}
			else
				hRemoteThread = Win32.CreateRemoteThread(this.hProc, IntPtr.Zero, 0, hFunc, hRemoteMem, Win32.CreateRemoteThreadCreationFlags.None, IntPtr.Zero);

			if (hRemoteThread == IntPtr.Zero)
				throw new Win32Exception(Marshal.GetLastWin32Error());

			return hRemoteThread;
		}

		#region CallFunction
		public IntPtr CallFunction(string Function)
		{
			return this.CallFunction(Function, IntPtr.Zero, Win32.INFINITE);
		}

		public IntPtr CallFunction(string Function, IntPtr hRemoteMem)
		{
			return this.CallFunction(Function, hRemoteMem, Win32.INFINITE);
		}

		public IntPtr CallFunction(string Function, uint MaxWait)
		{
			return this.CallFunction(Function, IntPtr.Zero, MaxWait);
		}

		public IntPtr CallFunction(string Function, IntPtr hRemoteMem, uint MaxWait)
		{
			if (Function == null)
				throw new ArgumentNullException("Function");

			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero
				|| this.hLibModule == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			if (this.hDll == IntPtr.Zero)
			{
				this.hDll = Win32.LoadLibrary(this.DllPath);

				if (this.hDll == IntPtr.Zero)
					throw new Exception("DllInjector.CallFunction failed, LoadLibrary failed", new Win32Exception(Marshal.GetLastWin32Error()));
			}

			IntPtr hFunc = Win32.GetProcAddress(this.hDll, Function);

			if (hFunc == IntPtr.Zero)
				throw new Exception("DllInjector.CallFunction failed, GetProcAddress failed", new Win32Exception(Marshal.GetLastWin32Error()));

			hFunc = new IntPtr(this.hLibModule.ToInt64() + (hFunc.ToInt64() - this.hDll.ToInt64()));
			return this.CallFunctionInternal(hFunc, hRemoteMem, MaxWait);
		}

		public IntPtr CallFunction(IntPtr hFunc, IntPtr hRemoteMem)
		{
			return this.CallFunction(hFunc, hRemoteMem, Win32.INFINITE);
		}

		public IntPtr CallFunction(IntPtr hFunc, IntPtr hRemoteMem, uint MaxWait)
		{
			if (hFunc == IntPtr.Zero)
				throw new ArgumentNullException("hFunc");

			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero
				|| this.hLibModule == IntPtr.Zero)
				throw new Exception("The dll hasn't been injected yet");

			return this.CallFunctionInternal(hFunc, hRemoteMem, MaxWait);
		}

		private IntPtr CallFunctionInternal(IntPtr hFunc, IntPtr hRemoteMem)
		{
			return this.CallFunctionInternal(hFunc, hRemoteMem, Win32.INFINITE);
		}

		private IntPtr CallFunctionInternal(IntPtr hFunc, IntPtr hRemoteMem, uint MaxWait)
		{
			if (hFunc == IntPtr.Zero)
				throw new ArgumentNullException("hFunc");

			if (this.hProc == IntPtr.Zero)
				throw new NullReferenceException("this.hProc");

			IntPtr hRemoteThread = IntPtr.Zero;

			try
			{
				hRemoteThread = this.CreateRemoteThread(hFunc, hRemoteMem);

				if (hRemoteThread != IntPtr.Zero)
				{
					if (Win32.WaitForSingleObject(hRemoteThread, MaxWait) == Win32.WaitForSingleObjectReturn.WAIT_TIMEOUT)
						throw new TimeoutException("DllInjector.CallFunctionInternal failed, Thread Timed Out", new Win32Exception(Marshal.GetLastWin32Error()));

					IntPtr lpExitCode;

					if (Win32.GetExitCodeThread(hRemoteThread, out lpExitCode))
						return lpExitCode;
					else
						throw new Win32Exception(Marshal.GetLastWin32Error(), "DllInjector.CallFunctionInternal failed, GetExitCodeThread failed");
				}
				else
					throw new Win32Exception(Marshal.GetLastWin32Error(), "DllInjector.CallFunctionInternal failed, CreateRemoteThread failed");
			}
			finally
			{
				if (hRemoteThread != IntPtr.Zero)
					Win32.CloseHandle(hRemoteThread);
			}
		}
		#endregion

		public bool Inject()
		{
			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (this.IsInjected)
				return true;

			IntPtr hRemoteMem = IntPtr.Zero;
			int lpNumBytesWritten = 0;

			try
			{
				if (!File.Exists(this.DllPath))
					throw new System.IO.FileNotFoundException(null, this.DllPath);

				if (this.TargetProcess == null)
					throw new NullReferenceException("this.TargetProcess");

				bool Is64bitProcess = Utilities.Is64BitProcess(this.TargetProcess);

				if (Utilities.IsDllArchMismatch(this.DllPath, Is64bitProcess))
				{
					Utilities.Log("DllInjector.Inject failed\r\n\r\n{0} is a {2} bit dll but {1} is {3} bit process.",
						this.DllPath,
						this.TargetProcess.ProcessName,
						Is64bitProcess ? "32" : "64",
						Is64bitProcess ? "64" : "32");
					return false;
				}

				IntPtr hLocKernel32 = Win32.GetModuleHandle("Kernel32.dll");

				if (hLocKernel32 == IntPtr.Zero)
					throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not find Kernel32");

				IntPtr hLocLoadLibrary = Win32.GetProcAddress(hLocKernel32, "LoadLibraryW");

				if (hLocLoadLibrary == IntPtr.Zero)
					throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not find the address of LoadLibraryW");

				try
				{
					Process.EnterDebugMode();
					DllInjector.DebugModeReferences++;
				}
				catch (Exception e)
				{
					Utilities.Log("DllInjector.Inject may fail if injecting into system process'.", e);
				}

				this.hProc = Win32.OpenProcess(Win32.ProcessAccess.PROCESS_CREATE_THREAD | Win32.ProcessAccess.PROCESS_VM_OPERATION | Win32.ProcessAccess.PROCESS_VM_WRITE | Win32.ProcessAccess.PROCESS_VM_READ | Win32.ProcessAccess.PROCESS_QUERY_INFORMATION, false, (uint)this.TargetProcess.Id);

				if (this.hProc == IntPtr.Zero)
					throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess failed");

				if (this.hDll == IntPtr.Zero)
				{
					this.hDll = Win32.LoadLibrary(this.DllPath);

					if (this.hDll == IntPtr.Zero)
						Utilities.Log(string.Format("DllInjector.CallFunction will fail, Could not load {0}", this.DllPath), new Win32Exception(Marshal.GetLastWin32Error()));
				}

				hRemoteMem = this.WriteStringUniInternal(this.DllPath, out lpNumBytesWritten);

				if (hRemoteMem == IntPtr.Zero)
					throw new Win32Exception(Marshal.GetLastWin32Error(), "DllInjector.WriteMemoryInternal failed");

				this.hLibModule = this.CallFunctionInternal(hLocLoadLibrary, hRemoteMem);

				if (IntPtr.Size != 4)
				{
					foreach (ProcessModule Module in this.TargetProcess.Modules)
					{
						if (string.Compare(Module.FileName, this.DllPath, StringComparison.InvariantCultureIgnoreCase) == 0
							&& Module.BaseAddress != IntPtr.Zero)
						{
							this.hLibModule = Module.BaseAddress;
							break;
						}
					}
				}

				this.IsInjected = (this.hLibModule != IntPtr.Zero);

				if (!this.IsInjected)
					throw new Exception("LoadLibrary failed");

				return this.IsInjected;
			}
			catch (Exception e)
			{
				Utilities.Log("DllInjector.Inject failed", e);
				return this.IsInjected;
			}
			finally
			{
				if (hRemoteMem != IntPtr.Zero)
					this.FreeMemoryInternal(hRemoteMem, lpNumBytesWritten);
			}
		}

		public bool Eject()
		{
			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			try
			{
				if (!this.IsInjected)
					return true;

				if (this.hProc == IntPtr.Zero
					|| this.hLibModule == IntPtr.Zero)
					throw new InvalidOperationException("Dll not injected");

				IntPtr hLocKernel32 = Win32.GetModuleHandle("Kernel32.dll");

				if (hLocKernel32 == IntPtr.Zero)
					throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not find Kernel32.dll");

				IntPtr hLocFreeLibrary = Win32.GetProcAddress(hLocKernel32, "FreeLibrary");

				if (hLocFreeLibrary == IntPtr.Zero)
					throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not find the address of FreeLibrary");

				this.IsInjected = (this.CallFunctionInternal(hLocFreeLibrary, this.hLibModule).ToInt32() != 1);

				if (!this.IsInjected
					&& --DllInjector.DebugModeReferences <= 0)
					Process.LeaveDebugMode();

				return !this.IsInjected;
			}
			catch (Exception e)
			{
				Utilities.Log("DllInjector.Eject failed", e);
				return !this.IsInjected;
			}
			finally
			{
				if (this.hDll != IntPtr.Zero)
					if (Win32.FreeLibrary(this.hDll))
						this.hDll = IntPtr.Zero;

				if (this.hProc != IntPtr.Zero)
					if (Win32.CloseHandle(this.hProc))
						this.hProc = IntPtr.Zero;
			}
		}

		#region ReadMemory
		public byte ReadByte(IntPtr lpBaseAddress)
		{
			return this.ReadMemory<byte>(lpBaseAddress);
		}

		public short ReadInt16(IntPtr lpBaseAddress)
		{
			return this.ReadMemory<short>(lpBaseAddress);
		}

		public int ReadInt32(IntPtr lpBaseAddress)
		{
			return this.ReadMemory<int>(lpBaseAddress);
		}

		public long ReadInt64(IntPtr lpBaseAddress)
		{
			return this.ReadMemory<long>(lpBaseAddress);
		}

		public IntPtr ReadIntPtr(IntPtr lpBaseAddress)
		{
			return this.ReadMemory<IntPtr>(lpBaseAddress);
		}

		public string ReadStringAnsi(IntPtr lpBaseAddress, out int lpNumberOfBytesRead)
		{
			if (lpBaseAddress == IntPtr.Zero)
				throw new ArgumentNullException("lpBaseAddress");

			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			List<byte> tempData = new List<byte>();
			int tempLpNumberOfBytesRead;

			for (long i = 0; ; i++)
			{
				byte tempByte = this.ReadMemoryInternal<byte>(new IntPtr(lpBaseAddress.ToInt64() + i), out tempLpNumberOfBytesRead);

				if (tempByte == 0)
					break;

				tempData.Add(tempByte);
			}

			lpNumberOfBytesRead = tempData.Count + 1;
			return Encoding.Default.GetString(tempData.ToArray());
		}

		public string ReadStringAnsi(IntPtr lpBaseAddress, int strLen)
		{
			byte[] Data = this.ReadMemory(lpBaseAddress, Encoding.Default.GetMaxByteCount(strLen));
			return Encoding.Default.GetString(Data, 0, Data.Length - Encoding.Default.GetMaxByteCount(0));
		}

		public string ReadStringAnsi(IntPtr lpBaseAddress, int strLen, out int lpNumberOfBytesRead)
		{
			byte[] Data = this.ReadMemory(lpBaseAddress, Encoding.Default.GetMaxByteCount(strLen), out lpNumberOfBytesRead);
			return Encoding.Default.GetString(Data, 0, Data.Length - Encoding.Default.GetMaxByteCount(0));
		}

		public string ReadStringUni(IntPtr lpBaseAddress, out int lpNumberOfBytesRead)
		{
			if (lpBaseAddress == IntPtr.Zero)
				throw new ArgumentNullException("lpBaseAddress");

			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			List<byte> tempData = new List<byte>();
			byte[] tempBytes = new byte[2];
			int tempLpNumberOfBytesRead;
			lpNumberOfBytesRead = 0;

			for (long i = 0; ; i += 2)
			{
				lpNumberOfBytesRead += (tempLpNumberOfBytesRead = this.ReadMemoryInternal(new IntPtr(lpBaseAddress.ToInt64() + i), tempBytes));

				if (tempLpNumberOfBytesRead != 2
					|| (tempBytes[0] == 0 && tempBytes[1] == 0))
					break;

				tempData.AddRange(tempBytes);
			}

			return Encoding.Unicode.GetString(tempData.ToArray());
		}

		public string ReadStringUni(IntPtr lpBaseAddress, int strLen)
		{
			byte[] Data = this.ReadMemory(lpBaseAddress, Encoding.Unicode.GetMaxByteCount(strLen));
			return Encoding.Unicode.GetString(Data, 0, Data.Length - Encoding.Unicode.GetMaxByteCount(0));
		}

		public string ReadStringUni(IntPtr lpBaseAddress, int strLen, out int lpNumberOfBytesRead)
		{
			byte[] Data = this.ReadMemory(lpBaseAddress, Encoding.Unicode.GetMaxByteCount(strLen), out lpNumberOfBytesRead);
			return Encoding.Unicode.GetString(Data, 0, Data.Length - Encoding.Unicode.GetMaxByteCount(0));
		}

		public T ReadMemory<T>(IntPtr lpBaseAddress)
			where T : struct
		{
			int lpNumberOfBytesRead;
			return this.ReadMemory<T>(lpBaseAddress, out lpNumberOfBytesRead);
		}

		public T ReadMemory<T>(IntPtr lpBaseAddress, out int lpNumberOfBytesRead)
			where T : struct
		{
			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			return this.ReadMemoryInternal<T>(lpBaseAddress, out lpNumberOfBytesRead);
		}

		private T ReadMemoryInternal<T>(IntPtr lpBaseAddress, out int lpNumberOfBytesRead)
			where T : struct
		{
			if (lpBaseAddress == IntPtr.Zero)
				throw new ArgumentNullException("lpBaseAddress");
			
			if (this.hProc == IntPtr.Zero)
				throw new NullReferenceException("this.hProc");
			
			int ResultSize = Marshal.SizeOf(typeof(T));
			IntPtr Result = Marshal.AllocHGlobal(ResultSize);

			try
			{
				if (!Win32.ReadProcessMemory(this.hProc, lpBaseAddress, Result, (uint)ResultSize, out lpNumberOfBytesRead))
					throw new Exception("DllInjector.ReadMemory failed, ReadProcessMemory failed", new Win32Exception(Marshal.GetLastWin32Error()));

				return (T)Marshal.PtrToStructure(Result, typeof(T));
			}
			finally
			{
				Marshal.FreeHGlobal(Result);
			}
		}

		public byte[] ReadMemory(IntPtr lpBaseAddress, int nSize)
		{
			byte[] ret = new byte[nSize];
			this.ReadMemory(lpBaseAddress, ret);
			return ret;
		}

		public byte[] ReadMemory(IntPtr lpBaseAddress, int nSize, out int lpNumberOfBytesRead)
		{
			byte[] ret = new byte[nSize];
			lpNumberOfBytesRead = this.ReadMemory(lpBaseAddress, ret);
			return ret;
		}

		public int ReadMemory(IntPtr lpBaseAddress, IntPtr lpBuffer, int nSize)
		{
			if (lpBaseAddress == IntPtr.Zero)
				throw new ArgumentNullException("lpBaseAddress");

			if (lpBuffer == null)
				throw new ArgumentNullException("lpBuffer");

			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			int lpNumberOfBytesRead;

			if (!Win32.ReadProcessMemory(this.hProc, lpBaseAddress, lpBuffer, (uint)nSize, out lpNumberOfBytesRead))
				throw new Exception("DllInjector.ReadMemory failed, ReadProcessMemory failed", new Win32Exception(Marshal.GetLastWin32Error()));

			return lpNumberOfBytesRead;
		}

		public int ReadMemory(IntPtr lpBaseAddress, [In, Out] byte[] lpBuffer)
		{
			if (lpBaseAddress == IntPtr.Zero)
				throw new ArgumentNullException("lpBaseAddress");

			if (lpBuffer == null)
				throw new ArgumentNullException("lpBuffer");

			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			int lpNumberOfBytesRead;

			if (!Win32.ReadProcessMemory(this.hProc, lpBaseAddress, lpBuffer, (uint)lpBuffer.Length, out lpNumberOfBytesRead))
				throw new Exception("DllInjector.ReadMemory failed, ReadProcessMemory failed", new Win32Exception(Marshal.GetLastWin32Error()));

			return lpNumberOfBytesRead;
		}

		private int ReadMemoryInternal(IntPtr lpBaseAddress, [In, Out] byte[] lpBuffer)
		{
			if (lpBaseAddress == IntPtr.Zero)
				throw new ArgumentNullException("lpBaseAddress");

			if (lpBuffer == null)
				throw new ArgumentNullException("lpBuffer");
			
			int lpNumberOfBytesRead;

			if (!Win32.ReadProcessMemory(this.hProc, lpBaseAddress, lpBuffer, (uint)lpBuffer.Length, out lpNumberOfBytesRead))
				throw new Exception("DllInjector.ReadMemory failed, ReadProcessMemory failed", new Win32Exception(Marshal.GetLastWin32Error()));

			return lpNumberOfBytesRead;
		}
		#endregion

		#region WriteMemory
		public IntPtr WriteByte(byte Value, out int lpNumBytesWritten)
		{
			return this.WriteMemory<byte>(Value, out lpNumBytesWritten);
		}

		public IntPtr WriteInt16(short Value, out int lpNumBytesWritten)
		{
			return this.WriteMemory<short>(Value, out lpNumBytesWritten);
		}

		public IntPtr WriteInt32(int Value, out int lpNumBytesWritten)
		{
			return this.WriteMemory<int>(Value, out lpNumBytesWritten);
		}

		public IntPtr WriteInt64(long Value, out int lpNumBytesWritten)
		{
			return this.WriteMemory<long>(Value, out lpNumBytesWritten);
		}

		public IntPtr WriteIntPtr(IntPtr Value, out int lpNumBytesWritten)
		{
			return this.WriteMemory<IntPtr>(Value, out lpNumBytesWritten);
		}

		public IntPtr WriteStringAnsi(string Value, out int lpNumBytesWritten)
		{
			return this.WriteMemory(Encoding.Default.GetBytes(Value + "\0"), out lpNumBytesWritten);
		}

		public IntPtr WriteStringAnsiInternal(string Value, out int lpNumBytesWritten)
		{
			return this.WriteMemoryInternal(Encoding.Default.GetBytes(Value + "\0"), out lpNumBytesWritten);
		}

		public IntPtr WriteStringUni(string Value, out int lpNumBytesWritten)
		{
			return this.WriteMemory(Encoding.Unicode.GetBytes(Value + "\0"), out lpNumBytesWritten);
		}

		public IntPtr WriteStringUniInternal(string Value, out int lpNumBytesWritten)
		{
			return this.WriteMemoryInternal(Encoding.Unicode.GetBytes(Value + "\0"), out lpNumBytesWritten);
		}

		public IntPtr WriteMemory<T>(T Data, out int lpNumBytesWritten)
			where T : struct
		{
			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			int DataLength = Marshal.SizeOf(typeof(T));
			IntPtr hRemoteMem = Win32.VirtualAllocEx(this.hProc, IntPtr.Zero, (uint)DataLength, Win32.VirtualAllocExAllocationType.MEM_COMMIT | Win32.VirtualAllocExAllocationType.MEM_RESERVE, Win32.MemoryProtection.PAGE_READWRITE);

			if (hRemoteMem == IntPtr.Zero)
				throw new Exception("DllInjector.WriteMemory failed, VirtualAllocEx failed", new Win32Exception(Marshal.GetLastWin32Error()));

			IntPtr ptr = Marshal.AllocHGlobal(DataLength);

			try
			{
				Marshal.StructureToPtr(Data, ptr, false);

				if (Win32.WriteProcessMemory(this.hProc, hRemoteMem, ptr, (uint)DataLength, out lpNumBytesWritten)
					&& hRemoteMem != IntPtr.Zero)
					return hRemoteMem;
				else
					throw new Exception("DllInjector.WriteMemory failed, WriteProcessMemory failed", new Win32Exception(Marshal.GetLastWin32Error()));
			}
			finally
			{
				Marshal.FreeHGlobal(ptr);
			}
		}

		public IntPtr WriteMemory([In] byte[] lpBuffer, out int lpNumBytesWritten)
		{
			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			return this.WriteMemoryInternal(lpBuffer, out lpNumBytesWritten);
		}

		public IntPtr WriteMemory(IntPtr lpBuffer, int nSize, out int lpNumBytesWritten)
		{
			if (lpBuffer == IntPtr.Zero)
				throw new ArgumentNullException("lpBuffer");

			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			IntPtr hRemoteMem = Win32.VirtualAllocEx(this.hProc, IntPtr.Zero, (uint)nSize, Win32.VirtualAllocExAllocationType.MEM_COMMIT | Win32.VirtualAllocExAllocationType.MEM_RESERVE, Win32.MemoryProtection.PAGE_READWRITE);

			if (hRemoteMem == IntPtr.Zero)
				throw new Exception("DllInjector.WriteMemory failed, VirtualAllocEx failed", new Win32Exception(Marshal.GetLastWin32Error()));

			if (Win32.WriteProcessMemory(this.hProc, hRemoteMem, lpBuffer, (uint)nSize, out lpNumBytesWritten)
				&& hRemoteMem != IntPtr.Zero)
				return hRemoteMem;
			else
				throw new Exception("DllInjector.WriteMemory failed, WriteProcessMemory failed", new Win32Exception(Marshal.GetLastWin32Error()));
		}

		private IntPtr WriteMemoryInternal([In] byte[] lpBuffer, out int lpNumBytesWritten)
		{
			if (lpBuffer == null)
				throw new ArgumentNullException("lpBuffer");

			if (this.hProc == IntPtr.Zero)
				throw new NullReferenceException("this.hProc");

			IntPtr hRemoteMem = Win32.VirtualAllocEx(this.hProc, IntPtr.Zero, (uint)lpBuffer.Length, Win32.VirtualAllocExAllocationType.MEM_COMMIT | Win32.VirtualAllocExAllocationType.MEM_RESERVE, Win32.MemoryProtection.PAGE_READWRITE);

			if (hRemoteMem == IntPtr.Zero)
				throw new Exception("DllInjector.WriteMemory failed, VirtualAllocEx failed", new Win32Exception(Marshal.GetLastWin32Error()));

			if (Win32.WriteProcessMemory(this.hProc, hRemoteMem, lpBuffer, (uint)lpBuffer.Length, out lpNumBytesWritten)
				&& hRemoteMem != IntPtr.Zero)
				return hRemoteMem;
			else
				throw new Exception("DllInjector.WriteMemory failed, WriteProcessMemory failed", new Win32Exception(Marshal.GetLastWin32Error()));
		}
		#endregion

		#region FreeMemory
		public bool FreeMemory(IntPtr lpBaseAddress, int dwSize)
		{
			if (this.Disposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!this.IsInjected
				|| this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("The dll hasn't been injected yet");

			return this.FreeMemoryInternal(lpBaseAddress, dwSize);
		}

		private bool FreeMemoryInternal(IntPtr lpBaseAddress, int dwSize)
		{
			if (lpBaseAddress == IntPtr.Zero)
				throw new ArgumentNullException("lpBaseAddress");

			if (this.hProc == IntPtr.Zero)
				throw new InvalidOperationException("this.hProc == IntPtr.Zero");

			return Win32.VirtualFreeEx(this.hProc, lpBaseAddress, dwSize, Win32.VirtualFreeExFreeType.MEM_RELEASE);
		}
		#endregion

		public void Dispose()
		{
			if (!this.Disposed)
			{
				if (this.EjectOnDispose)
					this.Eject();
				else
				{
					if (--DllInjector.DebugModeReferences <= 0)
						Process.LeaveDebugMode();

					if (this.hDll != IntPtr.Zero)
						if (Win32.FreeLibrary(this.hDll))
							this.hDll = IntPtr.Zero;

					if (this.hProc != IntPtr.Zero)
						if (Win32.CloseHandle(this.hProc))
							this.hProc = IntPtr.Zero;
				}

				this.Disposed = true;
			}
		}
		#endregion

		[DllImport("Ntdll.dll")]
		private static extern uint NtCreateThreadEx(out IntPtr hThread, uint DesiredAccess, IntPtr ObjectAttributes, IntPtr ProcessHandle, IntPtr lpStartAddress, IntPtr lpParameter, bool CreateSuspended, uint StackZeroBits, uint SizeOfStackCommit, uint SizeOfStackReserve, IntPtr lpBytesBuffer);
	}
}