using System;
using System.Runtime.InteropServices;

namespace Com.Xenthrax.WindowsDataVisualizer.Utilities
{
	internal static class Win32
	{
		[DllImport("Kernel32.dll", SetLastError = true)]
		public static extern IntPtr LoadLibrary(string dllFilePath);

		[DllImport("Kernel32.dll", SetLastError = true)]
		public static extern bool FreeLibrary(IntPtr hModule);

		[DllImport("Imagehlp.dll", SetLastError = true)]
		public static extern IntPtr ImageLoad(string DllName, string DllPath);

		[DllImport("Imagehlp.dll", SetLastError = true)]
		public static extern bool ImageUnload(IntPtr LoadedImage);

		[DllImport("Kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		[DllImport("Advapi32.dll", SetLastError = true)]
		public static extern bool CredEnumerate(string Filter, CredEnumerateFlags Flags, out uint Count, out IntPtr Credentials);

		[DllImport("Advapi32.dll", SetLastError = true)]
		public static extern void CredFree(IntPtr Buffer);

		[Flags]
		public enum CredEnumerateFlags : uint
		{
			None = 0,
			CRED_ENUMERATE_ALL_CREDENTIALS = 0x1
		}

		public struct LIST_ENTRY
		{
			public IntPtr Flink; //LIST_ENTRY
			public IntPtr Blink; //LIST_ENTRY
		}

		public struct LOADED_IMAGE
		{
			[MarshalAs(UnmanagedType.LPStr)]
			public string ModuleName;

			public IntPtr hFile;
			public IntPtr MappedAddress; //UCHAR
			public IntPtr FileHeader; //IMAGE_NT_HEADERS32
			public IntPtr LastRvaSection; //IMAGE_SECTION_HEADER
			public ulong NumberOfSections;
			public IntPtr Sections; //IMAGE_SECTION_HEADER
			public LOADED_IMAGE_Characteristics Characteristics;
			public bool fSystemImage;
			public bool fDOSImage;
			public bool fReadOnly;
			public byte Version; //UCHAR
			public LIST_ENTRY Links;
			public ulong SizeOfImage;
		}

		[Flags]
		public enum LOADED_IMAGE_Characteristics : ulong
		{
			IMAGE_FILE_RELOCS_STRIPPED = 0x1,
			IMAGE_FILE_EXECUTABLE_IMAGE = 0x2,
			IMAGE_FILE_LINE_NUMS_STRIPPED = 0x4,
			IMAGE_FILE_LOCAL_SYMS_STRIPPED = 0x8,
			IMAGE_FILE_AGGRESIVE_WS_TRIM = 0x10,
			IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x20,
			IMAGE_FILE_BYTES_REVERSED_LO = 0x80,
			IMAGE_FILE_32BIT_MACHINE = 0x100,
			IMAGE_FILE_DEBUG_STRIPPED = 0x200,
			IMAGE_FILE_REMOVABLE_RUN_FROM_SWAP = 0x400,
			IMAGE_FILE_NET_RUN_FROM_SWAP = 0x800,
			IMAGE_FILE_SYSTEM = 0x1000,
			IMAGE_FILE_DLL = 0x2000,
			IMAGE_FILE_UP_SYSTEM_ONLY = 0x4000,
			IMAGE_FILE_BYTES_REVERSED_HI = 0x8000
		}

		public struct IMAGE_NT_HEADERS
		{
			public uint Signature;
			public IMAGE_FILE_HEADER FileHeader;
			public IMAGE_OPTIONAL_HEADER OptionalHeader;
		}

		public struct IMAGE_OPTIONAL_HEADER
		{
			public IMAGE_OPTIONAL_HEADER_Magic Magic;
			public byte MajorLinkerVersion;
			public byte MinorLinkerVersion;
			public uint SizeOfCode;
			public uint SizeOfInitializedData;
			public uint SizeOfUninitializedData;
			public uint AddressOfEntryPoint;
			public uint BaseOfCode;
			public uint BaseOfData;
			public uint ImageBase;
			public uint SectionAlignment;
			public uint FileAlignment;
			public ushort MajorOperatingSystemVersion;
			public ushort MinorOperatingSystemVersion;
			public ushort MajorImageVersion;
			public ushort MinorImageVersion;
			public ushort MajorSubsystemVersion;
			public ushort MinorSubsystemVersion;
			public uint Win32VersionValue;
			public uint SizeOfImage;
			public uint SizeOfHeaders;
			public uint CheckSum;
			public IMAGE_OPTIONAL_HEADER_Subsystem Subsystem;
			public IMAGE_OPTIONAL_HEADER_DllCharacteristics DllCharacteristics;
			public uint SizeOfStackReserve;
			public uint SizeOfStackCommit;
			public uint SizeOfHeapReserve;
			public uint SizeOfHeapCommit;
			public uint LoaderFlags;
			public uint NumberOfRvaAndSizes;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
			public IMAGE_DATA_DIRECTORY[] DataDirectory;
		}

		public enum IMAGE_OPTIONAL_HEADER_Magic : ushort
		{
/*#if WIN_64
			IMAGE_NT_OPTIONAL_HDR_MAGIC = IMAGE_NT_OPTIONAL_HDR64_MAGIC,
#else
			IMAGE_NT_OPTIONAL_HDR_MAGIC = IMAGE_NT_OPTIONAL_HDR32_MAGIC,
#endif*/
			IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10,
			IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20,
			IMAGE_ROM_OPTIONAL_HDR_MAGIC = 0x107
		}

		public enum IMAGE_OPTIONAL_HEADER_Subsystem : ushort
		{
			IMAGE_SUBSYSTEM_UNKNOWN = 0,
			IMAGE_SUBSYSTEM_NATIVE = 1,
			IMAGE_SUBSYSTEM_WINDOWS_GUI = 2,
			IMAGE_SUBSYSTEM_WINDOWS_CUI = 3,
			IMAGE_SUBSYSTEM_OS2_CUI = 5,
			IMAGE_SUBSYSTEM_POSIX_CUI = 7,
			IMAGE_SUBSYSTEM_WINDOWS_CE_GUI = 9,
			IMAGE_SUBSYSTEM_EFI_APPLICATION = 10,
			IMAGE_SUBSYSTEM_EFI_BOOT_SERVICE_DRIVER = 11,
			IMAGE_SUBSYSTEM_EFI_RUNTIME_DRIVER = 12,
			IMAGE_SUBSYSTEM_EFI_ROM = 13,
			IMAGE_SUBSYSTEM_XBOX = 14,
			IMAGE_SUBSYSTEM_WINDOWS_BOOT_APPLICATION = 16
		}

		public enum IMAGE_OPTIONAL_HEADER_DllCharacteristics : ushort
		{
			IMAGE_DLLCHARACTERISTICS_DYNAMIC_BASE = 0x40,
			IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY = 0x80,
			IMAGE_DLLCHARACTERISTICS_NX_COMPAT = 0x100,
			IMAGE_DLLCHARACTERISTICS_NO_ISOLATION = 0x200,
			IMAGE_DLLCHARACTERISTICS_NO_SEH = 0x400,
			IMAGE_DLLCHARACTERISTICS_NO_BIND = 0x800,
			IMAGE_DLLCHARACTERISTICS_WDM_DRIVER = 0x2000,
			IMAGE_DLLCHARACTERISTICS_TERMINAL_SERVER_AWARE = 0x8000
		}

		public struct IMAGE_DATA_DIRECTORY
		{
			public uint VirtualAddress;
			public uint Size;
		}

		public struct IMAGE_FILE_HEADER
		{
			public IMAGE_FILE_HEADER_Machine Machine;
			public ushort NumberOfSections;
			public uint TimeDateStamp;
			public uint PointerToSymbolTable;
			public uint NumberOfSymbols;
			public ushort SizeOfOptionalHeader;
			public IMAGE_FILE_HEADER_Characteristics Characteristics;
		}

		public enum IMAGE_FILE_HEADER_Machine : ushort
		{
			IMAGE_FILE_MACHINE_I386 = 0x14c,
			IMAGE_FILE_MACHINE_IA64 = 0x200,
			IMAGE_FILE_MACHINE_AMD64 = 0x8664
		}

		[Flags]
		public enum IMAGE_FILE_HEADER_Characteristics : ushort
		{
			IMAGE_FILE_RELOCS_STRIPPED = 0x1,
			IMAGE_FILE_EXECUTABLE_IMAGE = 0x2,
			IMAGE_FILE_LINE_NUMS_STRIPPED = 0x4,
			IMAGE_FILE_LOCAL_SYMS_STRIPPED = 0x8,
			IMAGE_FILE_AGGRESIVE_WS_TRIM = 0x10,
			IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x20,
			IMAGE_FILE_BYTES_REVERSED_LO = 0x80,
			IMAGE_FILE_32BIT_MACHINE = 0x100,
			IMAGE_FILE_DEBUG_STRIPPED = 0x200,
			IMAGE_FILE_REMOVABLE_RUN_FROM_SWAP = 0x400,
			IMAGE_FILE_NET_RUN_FROM_SWAP = 0x800,
			IMAGE_FILE_SYSTEM = 0x1000,
			IMAGE_FILE_DLL = 0x2000,
			IMAGE_FILE_UP_SYSTEM_ONLY = 0x4000,
			IMAGE_FILE_BYTES_REVERSED_HI = 0x8000
		}

		public struct CREDENTIAL
		{
			public CREDENTIAL_Flags Flags;
			public CREDENTIAL_Type Type;
			public string TargetName;
			public string Comment;
			public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
			public uint CredentialBlobSize;
			public IntPtr CredentialBlob;
			public CREDENTIAL_Persist Persist;
			public uint AttributeCount;
			public IntPtr Attributes;
			public string TargetAlias;
			public string UserName;
		}

		[Flags]
		public enum CREDENTIAL_Flags : uint
		{
			CRED_FLAGS_PROMPT_NOW = 0x2,
			CRED_FLAGS_USERNAME_TARGET = 0x4
		}

		public enum CREDENTIAL_Type : uint
		{
			CRED_TYPE_GENERIC = 1,
			CRED_TYPE_DOMAIN_PASSWORD = 2,
			CRED_TYPE_DOMAIN_CERTIFICATE = 3,
			[Obsolete]
			CRED_TYPE_DOMAIN_VISIBLE_PASSWORD = 4,
			[Obsolete]
			CRED_TYPE_DOMAIN_EXTENDED = 6
		}

		public enum CREDENTIAL_Persist : uint
		{
			CRED_PERSIST_SESSION = 1,
			CRED_PERSIST_LOCAL_MACHINE = 2,
			CRED_PERSIST_ENTERPRISE = 3
		}
	}
}