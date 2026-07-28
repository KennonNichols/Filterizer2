using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

namespace Filterizer2;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	public App()
	{
		DispatcherUnhandledException += App_DispatcherUnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
	}
	
	private void App_DispatcherUnhandledException(object sender,
		DispatcherUnhandledExceptionEventArgs e)
	{
		ShowExceptionWindow(e.Exception, "Unhandled Exception");
	}
	
	private void TaskScheduler_UnobservedTaskException(object? sender,
		UnobservedTaskExceptionEventArgs e)
	{
		ShowExceptionWindow(e.Exception, "Unobserved Task Exception");
	}
	
	private void CurrentDomain_UnhandledException(object sender,
		UnhandledExceptionEventArgs e)
	{
		ShowExceptionWindow(e.ExceptionObject as Exception ?? new Exception("Null exception"), "Unhandled Exception In Domain");
	}

	public static void ShowExceptionWindow(Exception e, string name)
	{
		MessageBox.Show(e.ToString(), name);
	}
}