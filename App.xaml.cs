using System;
using System.Windows;

namespace Com.Xenthrax.WindowsDataVisualizer
{
	public partial class App : Application
	{
		private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			Utilities.Utilities.Log(e.Exception);
		}
	}
}