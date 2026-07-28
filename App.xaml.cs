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
		MessageBox.Show(e.Exception.ToString(), "Unhandled Exception");
	}
	
	private void TaskScheduler_UnobservedTaskException(object? sender,
		UnobservedTaskExceptionEventArgs e)
	{
		MessageBox.Show(e.Exception.ToString(), "Unobserved Task Exception");
	}
	
	private void CurrentDomain_UnhandledException(object sender,
		UnhandledExceptionEventArgs e)
	{
		MessageBox.Show(e.ExceptionObject.ToString(), "Unhandled Exception In Domain");
	}
}