using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace ThrottledApiCaller
{
	public class ApiCaller
	{
		private int limit;
		private int interval;
		private ConcurrentQueue<HttpRequestMessage> queue;
		private System.Timers.Timer timer;

		public ApiCaller(int maxLimit, int intervalPeriod)
		{
			limit = maxLimit;
			interval = intervalPeriod;
			queue = new ConcurrentQueue<HttpRequestMessage>();

			timer = new System.Timers.Timer();
			timer.AutoReset = true;
			timer.Elapsed += Timer_Elapsed;
			timer.Enabled = true;
			timer.Start();
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			timer.Stop();
			timer.Interval = interval;
			timer.Start();
			System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
			HttpClient httpClient = new HttpClient();
			watch.Start();
			int count = 0;
			while (count < limit)
			{
				if (watch.ElapsedMilliseconds < interval)
				{
					if (queue.Count > 0)
					{
						HttpRequestMessage request;
						if (queue.TryDequeue(out request))
						{
							count++;
							Console.WriteLine("        REQUEST: {0} being sent at {1} from thread {2}",
								request.Headers.GetValues("x-count").FirstOrDefault(),
								DateTime.Now,
								Thread.CurrentThread.ManagedThreadId);
							Log(httpClient.SendAsync(request));
						}
					}
				}
				else
				{
					break;
				}
			}

			watch.Stop();
			Console.WriteLine("        ** {0} requests sent in last window of {1} ms **", count, watch.ElapsedMilliseconds);
		}

		private async void Log(Task<HttpResponseMessage> responseMessage)
		{
			try
			{
			var response = await responseMessage;
			Console.WriteLine("                RESPONSE: Received {0} for {1} at {2} from thread {3}",
				response.StatusCode,
				response.RequestMessage.Headers.GetValues("x-count").FirstOrDefault(),
				DateTime.Now,
				Thread.CurrentThread.ManagedThreadId);
			}
			catch (HttpRequestException e)
			{
				Console.WriteLine("                RESPONSE: Received error {0} at {1} from thread {2}",
				e.HResult,
				DateTime.Now,
				Thread.CurrentThread.ManagedThreadId);
			}
		}
		public void EnqueueRequest(HttpRequestMessage request, int i)
		{
			request.Headers.Add("x-count", i.ToString());
			queue.Enqueue(request);
			Console.WriteLine("ENQUEUE: Request {0} received at {1} from thread {2}",
				i,
				DateTime.Now,
				Thread.CurrentThread.ManagedThreadId);
		}

		private void WaitForCompletion()
		{
			while (queue.Count > 0)
			{
				Thread.Sleep(6000);
			}
		}
		static void Main()
		{
			Random rand = new Random();
			ApiCaller caller = new ApiCaller(5, 10 * 1000); //set request sending limit to 5 per 10 seconds
			for (int i = 1; i <= 60; i++)
			{
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, @"https://localhost");
				caller.EnqueueRequest(request, i);
			}
			caller.WaitForCompletion();
		}


	}
}
