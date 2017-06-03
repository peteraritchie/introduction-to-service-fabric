using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace VoteTracking.Controllers
{
	[Route("api/[controller]")]
	public class VotesController : Controller
	{
		// Used for health checks.
		public static long _requestCount = 0L;

		// Holds the votes and counts. NOTE: THIS IS NOT THREAD SAFE DEMO ONLY.
		static Dictionary<string, int> _counts = new Dictionary<string, int>();

		/// GET api/votes/5
		[HttpGet("{key}")]
		public string Get(string key)
		{
#if !HEALTH_DISABLED
			string activityId = Guid.NewGuid().ToString();
			ServiceEventSource.Current.ServiceRequestStart($"VotesController.{nameof(Get)}", activityId);
#endif
			if (string.IsNullOrEmpty(key))
			{
				HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				return null;
			}
#if false
			if (key.Length > 10)
			{
				HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				return null;
			}
#endif
			Interlocked.Increment(ref _requestCount);
			if (!_counts.ContainsKey(key))
			{
				HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
				ServiceEventSource.Current.ServiceRequestStop($"VotesController.{nameof(Get)}", activityId);
				return null;
			}
#if !HEALTH_DISABLED
			ServiceEventSource.Current.ServiceRequestStop($"VotesController.{nameof(Get)}", activityId);
#endif
			return _counts[key].ToString(CultureInfo.InvariantCulture);
		}

		/// GET api/votes
		[HttpGet]
		public List<KeyValuePair<string, int>> Get()
		{
#if !HEALTH_DISABLED
			string activityId = Guid.NewGuid().ToString();
			ServiceEventSource.Current.ServiceRequestStart($"VotesController.{nameof(Get)}", activityId);
#endif
			Interlocked.Increment(ref _requestCount);

			var votes = new List<KeyValuePair<string, int>>(_counts.Count);
			foreach (var kvp in _counts)
			{
				votes.Add(kvp);
			}

#if !HEALTH_DISABLED
			ServiceEventSource.Current.ServiceRequestStop($"VotesController.{nameof(Get)}", activityId);
#endif
			return votes;
		}

		// POST api/votes
		[HttpPost]
		public void Post([FromBody] string key)
		{
#if !HEALTH_DISABLED
			string activityId = Guid.NewGuid().ToString();
			ServiceEventSource.Current.ServiceRequestStart($"VotesController.{nameof(Post)}", activityId);
#endif
			Interlocked.Increment(ref _requestCount);

			if (false == _counts.ContainsKey(key))
			{
				_counts.Add(key, 1);
			}
			else
			{
				_counts[key] = _counts[key] + 1;
			}
			HttpContext.Response.StatusCode = (int) HttpStatusCode.NoContent;
#if !HEALTH_DISABLED
			ServiceEventSource.Current.ServiceRequestStop($"VotesController.{nameof(Post)}", activityId);
#endif
		}

		// DELETE api/votes/5
		[HttpDelete("{key}")]
		public void Delete(string key)
		{
#if !HEALTH_DISABLED
			string activityId = Guid.NewGuid().ToString();
			ServiceEventSource.Current.ServiceRequestStart($"VotesController.{nameof(Delete)}", activityId);
#endif
			Interlocked.Increment(ref _requestCount);

			if (_counts.ContainsKey(key))
			{
				if (_counts.Remove(key))
				{
					ServiceEventSource.Current.ServiceRequestStop($"VotesController.{nameof(Delete)}", activityId);
					return;
				}
			}
			HttpContext.Response.StatusCode = (int) HttpStatusCode.NotFound;
#if !HEALTH_DISABLED
			ServiceEventSource.Current.ServiceRequestStop($"VotesController.{nameof(Delete)}", activityId);
#endif
		}
	}
}
