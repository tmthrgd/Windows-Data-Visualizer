using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Com.Xenthrax.WindowsDataVisualizer.Utilities
{
	//http://bytes.com/topic/net/insights/797169-reading-parsing-ini-file-c
	internal class IniParser : IEnumerable<KeyValuePair<string, IDictionary<string, string>>>
	{
		private const string ROOT = "____ROOT____";

		private NoThrowDictionary<string, IDictionary<string, string>> Sections = new NoThrowDictionary<string, IDictionary<string, string>>()
		{
			{ IniParser.ROOT, new NoThrowDictionary<string, string>() }
		};

		private string FilePath;

		/// <summary>
		/// Returns the value for the given section, key pair.
		/// </summary>
		/// <param name="sectionName">Site name.</param>
		/// <param name="settingName">Key name.</param>
		public string this[string Section, string Setting]
		{
			get
			{
				return this[Section][Setting];
			}
			set
			{
				this[Section][Setting] = value;
			}
		}

		/// <summary>
		/// Enumerates all lines for given section.
		/// </summary>
		/// <param name="sectionName">Site to enum.</param>
		public IDictionary<string, string> this[string Section]
		{
			get
			{
				if (string.IsNullOrEmpty(Section))
					Section = IniParser.ROOT;

				return (this.Sections[Section] != null)
					? this.Sections[Section]
					: this.Sections[Section] = new NoThrowDictionary<string, string>();
			}
		}

		/// <summary>
		/// Opens the INI file at the given path and enumerates the values in the IniParser.
		/// </summary>
		/// <param name="iniPath">Full path to INI file.</param>
		public IniParser(string FilePath)
		{
			this.FilePath = FilePath;

			if (!File.Exists(this.FilePath))
				throw new FileNotFoundException("Unable to locate ini file.", this.FilePath);

			using (StreamReader INIFile = File.OpenText(FilePath))
			{
				string CurrentRoot = IniParser.ROOT;
				string Line = INIFile.ReadLine();

				while (Line != null)
				{
					string LineTrimmed = Line.Trim();

					if (!string.IsNullOrWhiteSpace(Line))
					{
						if (LineTrimmed[0] == '[' && LineTrimmed[LineTrimmed.Length - 1] == ']')
							CurrentRoot = LineTrimmed.Substring(1, LineTrimmed.Length - 2).Trim();
						else
						{
							string[] KeyPair = Line.Split(new char[] { '=' }, 2);
							this[CurrentRoot][KeyPair[0].Trim()] = (KeyPair.Length > 1) ? KeyPair[1] : null;
						}
					}

					Line = INIFile.ReadLine();
				}
			}
		}

		/// <summary>
		/// Returns the value for the given section, key pair.
		/// </summary>
		/// <param name="sectionName">Site name.</param>
		/// <param name="settingName">Key name.</param>
		public string GetSetting(string Section, string Setting)
		{
			return this.Sections[string.IsNullOrEmpty(Section) ? IniParser.ROOT : Section][Setting];
		}

		/// <summary>
		/// Enumerates all lines for given section.
		/// </summary>
		/// <param name="sectionName">Site to enum.</param>
		public IDictionary<string, string> EnumSection(string Section)
		{
			return this.Sections[string.IsNullOrEmpty(Section) ? IniParser.ROOT : Section];
		}

		/// <summary>
		/// Returns a list of sections present.
		/// </summary>
		public string[] EnumSections()
		{
			return this.Sections.Keys.ToArray();
		}

		/// <summary>
		/// Adds or replaces a setting to the table to be saved.
		/// </summary>
		/// <param name="sectionName">Site to add under.</param>
		/// <param name="settingName">Key name to add.</param>
		/// <param name="settingValue">Value of key.</param>
		public void AddSetting(string SectionName, string SettingName, string SettingValue)
		{
			this[SectionName][SettingName] = SettingValue;
		}

		/// <summary>
		/// Adds or replaces a setting to the table to be saved with a null value.
		/// </summary>
		/// <param name="sectionName">Site to add under.</param>
		/// <param name="settingName">Key name to add.</param>
		public void AddSetting(string SectionName, string SettingName)
		{
			this.AddSetting(SectionName, SettingName, null);
		}

		/// <summary>
		/// Remove a setting.
		/// </summary>
		/// <param name="sectionName">Site to add under.</param>
		/// <param name="settingName">Key name to add.</param>
		public void DeleteSetting(string SectionName, string SettingName)
		{
			if (this.Sections.ContainsKey(SectionName)
				&& this.Sections[SectionName].ContainsKey(SettingName))
				this.Sections[SectionName].Remove(SettingName);
		}

		/// <summary>
		/// Save settings to new file.
		/// </summary>
		/// <param name="newFilePath">New file path.</param>
		public void SaveSettings(string FilePath)
		{
			if (File.Exists(FilePath))
				File.Delete(FilePath);

			using (StreamWriter INIFile = File.CreateText(FilePath))
			{
				foreach (KeyValuePair<string, IDictionary<string, string>> Section in this.Sections)
				{
					if (Section.Key != IniParser.ROOT)
						INIFile.WriteLine("[{0}]", Section);

					foreach (KeyValuePair<string, string> Pair in Section.Value)
						if (Pair.Value == null)
							INIFile.WriteLine(Pair.Key);
						else
							INIFile.WriteLine("{0}={1}", Pair.Key, Pair.Value);

					INIFile.WriteLine();
				}
			}
		}

		/// <summary>
		/// Save settings back to ini file.
		/// </summary>
		public void SaveSettings()
		{
			this.SaveSettings(this.FilePath);
		}

		public IEnumerator<KeyValuePair<string, IDictionary<string, string>>> GetEnumerator()
		{
			return this.Sections.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}
}