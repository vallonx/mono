using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace WsProxy
{
	public class ProxyOptions {
		public Uri DevToolsUrl { get; set; } = new Uri ("http://localhost:9222");
	}

	public class TestHarnessOptions : ProxyOptions {
		public string ChromePath { get; set; }
		public string AppPath { get; set; }
		public string PagePath { get; set; }
		public string NodeApp { get; set; }
	}

	public class Program {
		public static void Main(string[] args)
		{
			var host = new WebHostBuilder()
				.UseSetting ("UseIISIntegration", false.ToString ())
				.UseKestrel ()
				.UseContentRoot (Directory.GetCurrentDirectory())
				.UseStartup<Startup> ()
				.ConfigureAppConfiguration ((hostingContext, config) =>
				{
					config.AddCommandLine(args);
				})
				.UseUrls ("http://localhost:9300")
				.Build ();

			host.Run ();
		}
	}

	public class TestHarnessProxy {
		static IWebHost host;
		static Task hostTask;
		static CancellationTokenSource cts = new CancellationTokenSource ();
		static object proxyLock = new object ();

		public static readonly Uri Endpoint = new Uri ("http://localhost:9400");

		public static Task Start (string chromePath, string appPath, string pagePath)
		{
			lock (proxyLock) {
				if (host != null)
					return hostTask;

				host = new WebHostBuilder()
					.UseSetting ("UseIISIntegration", false.ToString ())
					.ConfigureServices (services => {
						services.Configure<TestHarnessOptions> (options => {
							options.ChromePath = chromePath;
							options.AppPath = appPath;
							options.PagePath = pagePath;
							options.DevToolsUrl = new Uri ("http://localhost:9333");
						});
					})
					.UseKestrel ()
					.UseStartup<TestHarnessStartup> ()
					.UseUrls (Endpoint.ToString ())
					.Build();
				hostTask = host.StartAsync (cts.Token);
			}

			Console.WriteLine ("WebServer Ready!");
			return hostTask;
		}
	}
}
