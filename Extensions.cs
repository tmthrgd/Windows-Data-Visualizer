using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Threading;

namespace Com.Xenthrax.WindowsDataVisualizer
{
	internal static class Extensions
	{
		public static IEnumerable<T> Convert<T>(this IEnumerable enumerable)
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");

			foreach (object value in enumerable)
				yield return (T)System.Convert.ChangeType(value, typeof(T));
		}

		public static void AddRange<T>(this ICollection<T> list, params T[] value)
		{
			if (list == null)
				throw new ArgumentNullException("list");

			if (value == null)
				throw new ArgumentNullException("value");

			foreach (T item in value)
				list.Add(item);
		}

		public static void AddRange<T>(this List<T> list, params T[] value)
		{
			if (list == null)
				throw new ArgumentNullException("list");

			if (value == null)
				throw new ArgumentNullException("value");

			list.AddRange((IEnumerable<T>)value);
		}

		public static void RemoveFirst<T>(this List<T> list)
		{
			if (list == null)
				throw new ArgumentNullException("list");

			list.RemoveAt(0);
		}

		public static void RemoveLast<T>(this List<T> list)
		{
			if (list == null)
				throw new ArgumentNullException("list");

			list.RemoveAt(list.Count - 1);
		}

		public static object Default(this Type type)
		{
			if (type == null)
				throw new ArgumentNullException("type");

			return type.IsValueType ? Activator.CreateInstance(type) : null;
		}

		public static IEnumerable<T> ReturnEnumerable<T>(params T[] items)
		{
			if (items == null)
				throw new ArgumentNullException("items");

			return items;
		}

		#region DeferExecution/DeferExecutionOnce
		public static IEnumerable<TResult> DeferExecution<TResult>(this Func<IEnumerable<TResult>> func)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(func);
		}

		public static IEnumerable<TResult> DeferExecution<T, TResult>(this Func<T, IEnumerable<TResult>> func, T arg)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(() => func(arg));
		}

		public static IEnumerable<TResult> DeferExecution<T1, T2, TResult>(this Func<T1, T2, IEnumerable<TResult>> func, T1 arg1, T2 arg2)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(() => func(arg1, arg2));
		}

		public static IEnumerable<TResult> DeferExecution<T1, T2, T3, TResult>(this Func<T1, T2, T3, IEnumerable<TResult>> func, T1 arg1, T2 arg2, T3 arg3)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(() => func(arg1, arg2, arg3));
		}

		public static IEnumerable<TResult> DeferExecution<T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, IEnumerable<TResult>> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(() => func(arg1, arg2, arg3, arg4));
		}

		public static IEnumerable<TResult> DeferExecutionOnce<TResult>(this Func<IEnumerable<TResult>> func)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(func).Memoize();
		}

		public static IEnumerable<TResult> DeferExecutionOnce<T, TResult>(this Func<T, IEnumerable<TResult>> func, T arg)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(() => func(arg)).Memoize();
		}

		public static IEnumerable<TResult> DeferExecutionOnce<T1, T2, TResult>(this Func<T1, T2, IEnumerable<TResult>> func, T1 arg1, T2 arg2)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(() => func(arg1, arg2)).Memoize();
		}

		public static IEnumerable<TResult> DeferExecutionOnce<T1, T2, T3, TResult>(this Func<T1, T2, T3, IEnumerable<TResult>> func, T1 arg1, T2 arg2, T3 arg3)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(() => func(arg1, arg2, arg3)).Memoize();
		}

		public static IEnumerable<TResult> DeferExecutionOnce<T1, T2, T3, T4, TResult>(this Func<T1, T2, T3, T4, IEnumerable<TResult>> func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
		{
			if (func == null)
				throw new ArgumentNullException("func");

			return new DeferExecutionEnumerable<TResult>(() => func(arg1, arg2, arg3, arg4)).Memoize();
		}
		#endregion

		public static IEnumerable<T> AsSerializable<T>(this IEnumerable<T> enumerable)
		{
			if (enumerable == null)
				throw new ArgumentNullException("enumerable");

			if (enumerable is SerializableEnumerable<T>)
				return enumerable;
			else
				return new SerializableEnumerable<T>(enumerable);
		}

		private class DeferExecutionEnumerable<T> : IEnumerable<T>, IEnumerator<T>
		{
			private Func<IEnumerable<T>> func;
			
			public DeferExecutionEnumerable(Func<IEnumerable<T>> func)
			{
				this.func = func;
			}

			// IEnumerable<T>
			public IEnumerator<T> GetEnumerator()
			{
				return new DeferExecutionEnumerable<T>(this.func);
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}

			// IEnumerator<T>
			private IEnumerator<T> enumerator;
			
			public T Current
			{
				get { return this.enumerator.Current; }
			}

			public void Dispose()
			{
				if (this.enumerator != null)
					this.enumerator.Dispose();

				this.enumerator = null;
			}

			object System.Collections.IEnumerator.Current
			{
				get { return this.enumerator.Current; }
			}

			public bool MoveNext()
			{
				if (this.enumerator == null)
					this.enumerator = this.func().GetEnumerator();

				return this.enumerator.MoveNext();
			}

			public void Reset()
			{
				if (this.enumerator != null)
					this.enumerator.Reset();
			}
		}
		
		[CollectionDataContract]
		private class SerializableEnumerable<T> : IEnumerable<T>
		{
			private IEnumerable<T> enumerable;
			private List<T> list;

			private SerializableEnumerable()
			{
				this.enumerable = this.list = new List<T>();
			}

			public SerializableEnumerable(IEnumerable<T> enumerable)
			{
				this.enumerable = enumerable;
			}

			public IEnumerator<T> GetEnumerator()
			{
				return this.enumerable.GetEnumerator();
			}
			
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.enumerable.GetEnumerator();
			}

			private void Add(T value)
			{
				this.list.Add(value);
			}
		}
	}
}