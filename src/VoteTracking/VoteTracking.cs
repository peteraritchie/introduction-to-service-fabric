using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace VoteTracking
{
	/// <summary>
	/// The FabricRuntime creates an instance of this class for each service type instance. 
	/// </summary>
	internal sealed class VoteTracking : StatelessService
	{
#if !HEALTH_DISABLED
		private TimeSpan _interval = TimeSpan.FromSeconds(30);
		private long _lastCount = 0L;
		private DateTime _lastReport = DateTime.UtcNow;
		private Timer _healthTimer = null;
		private FabricClient _client = null;
#endif
		public VoteTracking(StatelessServiceContext context)
			: base(context)
		{
#if !HEALTH_DISABLED
			// Create the timer here, so we can do a change operation on it later, avoiding creating/disposing of the 
			// timer.
			_healthTimer = new Timer(ReportHealthAndLoad, null, Timeout.Infinite, Timeout.Infinite);
#endif
		}

		// PR
		protected override Task OnOpenAsync(CancellationToken cancellationToken)
		{
#if !HEALTH_DISABLED
			_client = new FabricClient();
			_healthTimer.Change(_interval, _interval);
#endif
			return base.OnOpenAsync(cancellationToken);
		}
#if !HEALTH_DISABLED
		public void ReportHealthAndLoad(object notused)
		{
			// Calculate the values and then remember current values for the next report.
			long total = Controllers.VotesController._requestCount;
			long diff = total - _lastCount;
			long duration = Math.Max((long) DateTime.UtcNow.Subtract(_lastReport).TotalSeconds, 1L);
			long rps = diff / duration;
			_lastCount = total;
			_lastReport = DateTime.UtcNow;

			// Create the health information for this instance of the service and send report to Service Fabric.
			var hi = new HealthInformation("VotingServiceHealth", "Heartbeat", HealthState.Ok)
			{
				TimeToLive = _interval.Add(_interval),
				Description = $"{diff} requests since last report. RPS: {rps} Total requests: {total}.",
				RemoveWhenExpired = false,
				SequenceNumber = HealthInformation.AutoSequenceNumber
			};
			var sshr = new StatelessServiceInstanceHealthReport(Context.PartitionId, Context.InstanceId, hi);
			_client.HealthManager.ReportHealth(sshr);

			// Report the load
			Partition.ReportLoad(new[] {new LoadMetric("RPS", (int) rps)});
#if BAD_HEALTH_ENABLED
			// Report failing health report to cause rollback.
			var nodeList = _client.QueryManager.GetNodeListAsync(Context.NodeContext.NodeName).GetAwaiter().GetResult();
			var node = nodeList[0];
			if ("4" == node.UpgradeDomain || "3" == node.UpgradeDomain || "2" == node.UpgradeDomain)
			{
				hi = new HealthInformation("VotingServiceHealth", "Error_Heartbeat", HealthState.Error);
				hi.TimeToLive = _interval.Add(_interval);
				hi.Description = $"Bogus health error to force rollback.";
				hi.RemoveWhenExpired = true;
				hi.SequenceNumber = HealthInformation.AutoSequenceNumber;
				sshr = new StatelessServiceInstanceHealthReport(Context.PartitionId, Context.InstanceId, hi);
				_client.HealthManager.ReportHealth(sshr);
			}
#endif
		}
#endif
			/// <summary>
			/// Optional override to create listeners (like tcp, http) for this service instance.
			/// </summary>
			/// <returns>The collection of listeners.</returns>
		protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
		{
			return new[]
			{
				new ServiceInstanceListener(serviceContext =>
					new WebListenerCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
					{
						ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting WebListener on {url}");

						return new WebHostBuilder().UseWebListener()
							.ConfigureServices(
								services => services
									.AddSingleton<StatelessServiceContext>(serviceContext))
							.UseContentRoot(Directory.GetCurrentDirectory())
							.UseStartup<Startup>()
							.UseApplicationInsights()
							.UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
							.UseUrls(url)
							.Build();
					}))
			};
		}
	}
}