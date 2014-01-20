using System;

namespace FE3458D878534D9183D79D9318BB08C0.Utilities
{
	public class WMIDateTime
	{
		protected DateTime _Value;

		public WMIDateTime()
		{
			this._Value = new DateTime(0, DateTimeKind.Unspecified);
		}

		public WMIDateTime(DateTime Value)
		{
			this._Value = Value;
		}

		public WMIDateTime(long Ticks)
		{
			this._Value = new DateTime(Ticks);
		}

		public WMIDateTime(long Ticks, DateTimeKind kind)
		{
			this._Value = new DateTime(Ticks, kind);
		}

		public WMIDateTime(int Year, int Month, int Day)
		{
			this._Value = new DateTime(Year, Month, Day);
		}

		public WMIDateTime(int Year, int Month, int Day, int Hour, int Minute, int Second)
		{
			this._Value = new DateTime(Year, Month, Day, Hour, Minute, Second);
		}

		public WMIDateTime(int Year, int Month, int Day, int Hour, int Minute, int Second, DateTimeKind kind)
		{
			this._Value = new DateTime(Year, Month, Day, Hour, Minute, Second, kind);
		}

		public WMIDateTime(int Year, int Month, int Day, int Hour, int Minute, int Second, int MilliSecond)
		{
			this._Value = new DateTime(Year, Month, Day, Hour, Minute, Second, MilliSecond);
		}

		public WMIDateTime(int Year, int Month, int Day, int Hour, int Minute, int Second, int MilliSecond, DateTimeKind kind)
		{
			this._Value = new DateTime(Year, Month, Day, Hour, Minute, Second, MilliSecond, kind);
		}

		public override string ToString()
		{
			TimeSpan Offset = (this._Value.ToUniversalTime() - this._Value);
			return this._Value.ToString("yyyyMMddHHmmss.ffffff")
				//+ (DateTime.Compare(this._Value, this._Value.ToUniversalTime()) == -1 ? "-" : "+")
				+ (Offset >= TimeSpan.Zero ? "+" : "-")
				+ string.Format("{0:000}", Math.Max(Offset.TotalMinutes, -Offset.TotalMinutes));
		}

		public static WMIDateTime Parse(string s)
		{
			if (s == null)
				throw new ArgumentNullException("s");

			if (s.Length != 25)
				throw new ArgumentException("Invalid length", "s");

			int Year = DateTime.UtcNow.Year;
			string TempString = s.Substring(0, 4);

			if (TempString != "****")
				Year = int.Parse(TempString);

			int Month = 1;
			TempString = s.Substring(4, 2);

			if (TempString != "**")
				Month = int.Parse(TempString);

			int Day = 1;
			TempString = s.Substring(6, 2);

			if (TempString != "**")
				Day = int.Parse(TempString);

			int Hour = 0;
			TempString = s.Substring(8, 2);

			if (TempString != "**")
				Hour = int.Parse(TempString);

			int Minute = 0;
			TempString = s.Substring(10, 2);

			if (TempString != "**")
				Minute = int.Parse(TempString);

			int Second = 0;
			TempString = s.Substring(12, 2);

			if (TempString != "**")
				Second = int.Parse(TempString);

			int Milli = 0;
			TempString = s.Substring(15, 3);

			if (TempString != "***")
				Milli = int.Parse(TempString);

			return new WMIDateTime(Year, Month, Day, Hour, Minute, Second, Milli, DateTimeKind.Utc);
		}

		public static bool TryParse(string s, out WMIDateTime result)
		{
			result = null;

			if (s == null || s.Length != 25)
				return false;

			int Year = DateTime.UtcNow.Year;
			string TempString = s.Substring(0, 4);

			if (TempString != "****"
				&& !int.TryParse(TempString, out Year))
				return false;

			int Month = 1;
			TempString = s.Substring(4, 2);

			if (TempString != "**"
				&& !int.TryParse(TempString, out Month))
				return false;

			int Day = 1;
			TempString = s.Substring(6, 2);

			if (TempString != "**"
				&& !int.TryParse(TempString, out Day))
				return false;

			int Hour = 0;
			TempString = s.Substring(8, 2);

			if (TempString != "**"
				&& !int.TryParse(TempString, out Hour))
				return false;

			int Minute = 0;
			TempString = s.Substring(10, 2);

			if (TempString != "**"
				&& !int.TryParse(TempString, out Minute))
				return false;

			int Second = 0;
			TempString = s.Substring(12, 2);

			if (TempString != "**"
				&& !int.TryParse(TempString, out Second))
				return false;

			int Milli = 0;
			TempString = s.Substring(15, 3);

			if (TempString != "***"
				&& !int.TryParse(TempString, out Milli))
				return false;

			result = new WMIDateTime(Year, Month, Day, Hour, Minute, Second, Milli, DateTimeKind.Utc);
			return true;
		}

		private static WMIDateTime _MinValue;
		private static WMIDateTime _MaxValue;

		public static WMIDateTime MinValue
		{
			get
			{
				if (WMIDateTime._MinValue == null)
					WMIDateTime._MinValue = new WMIDateTime(DateTime.MinValue);

				return WMIDateTime._MinValue;
			}
		}

		public static WMIDateTime MaxValue
		{
			get
			{
				if (WMIDateTime._MaxValue == null)
					WMIDateTime._MaxValue = new WMIDateTime(DateTime.MaxValue);

				return WMIDateTime._MaxValue;
			}
		}

		public static implicit operator WMIDateTime(DateTime d)
		{
			return new WMIDateTime(d);
		}

		public static implicit operator DateTime(WMIDateTime d)
		{
			return d._Value;
		}
	}
}