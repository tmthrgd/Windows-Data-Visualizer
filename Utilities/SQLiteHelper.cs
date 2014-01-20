using System;
using System.IO;
using System.Data.SQLite;

namespace Com.Xenthrax.WindowsDataVisualizer.Utilities
{
	internal class SQLiteHelper : IDisposable
	{
		private string TempFile;
		public SQLiteConnection Connection { get; private set; }

		public SQLiteHelper(string DBPath)
		{
			this.TempFile = Path.GetTempFileName();
			File.Copy(DBPath, this.TempFile, true);

			SQLiteConnectionStringBuilder SQLiteConnectionString = new SQLiteConnectionStringBuilder();
			SQLiteConnectionString.Add("Data Source", this.TempFile);

			this.Connection = new SQLiteConnection(SQLiteConnectionString.ToString());
			this.Connection.Open();
		}

		public void ForEach(string Query, Action<SQLiteDataReader> Callback)
		{
			using (SQLiteCommand Command = this.Connection.CreateCommand())
			{
				Command.CommandText = Query;

				using (SQLiteDataReader DataReader = Command.ExecuteReader())
				{
					while (DataReader.Read())
					{
						try
						{
							Callback.Invoke(DataReader);
						}
						catch (Exception e)
						{
							Utilities.Log(e);
						}
					}
				}
			}
		}

		public void Close()
		{
			if (this.Connection != null)
				this.Connection.Close();

			if (this.TempFile != null)
				File.Delete(this.TempFile);
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool Disposing)
		{
			if (Disposing)
			{
				this.Close();

				if (this.Connection != null)
				{
					this.Connection.Dispose();
					this.Connection = null;
				}
			}
		}
	}
}