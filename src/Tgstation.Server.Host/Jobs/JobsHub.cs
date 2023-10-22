using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;

using Tgstation.Server.Host.Database;
using Tgstation.Server.Host.Models;
using Tgstation.Server.Host.Security;

namespace Tgstation.Server.Host.Jobs
{
	/// <summary>
	/// A SignalR <see cref="Hub"/> for pushing job updates.
	/// </summary>
	public class JobsHub : Hub
	{
		readonly IDatabaseContext databaseContext;
		readonly IAuthenticationContext authenticationContext;

		public JobsHub(IDatabaseContext databaseContext, IAuthenticationContext authenticationContext)
		{
			this.databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
			this.authenticationContext = authenticationContext ?? throw new ArgumentNullException(nameof(authenticationContext));
		}

		static string InstanceGroupName(long instanceId)
			=> $"instance-{instanceId}";

		static string InstanceGroupName(Instance instance)
			=> InstanceGroupName(instance.Id.Value);

		static string JobInstanceGroupName(Job job)
		{
			ArgumentNullException.ThrowIfNull(job);
			if (job.Instance == null)
				throw new InvalidOperationException("job.Instance was null!");

			return InstanceGroupName(job.Instance);
		}

		public Task PushUpdate(Job job, CancellationToken cancellationToken)
			=> Clients.Group(JobInstanceGroupName(job)).SendAsync("jobUpdate", cancellationToken);

		public async ValueTask RegisterWithInstance(long instanceId, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
			await Groups.AddToGroupAsync(Context.ConnectionId, InstanceGroupName(instanceId), cancellationToken);
		}

		public async ValueTask UnregisterUserWithInstance(User user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}
	}
}
