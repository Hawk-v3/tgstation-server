﻿using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;

namespace Tgstation.Server.Client.Components
{
	/// <summary>
	/// For managing <see cref="DreamDaemon"/>
	/// </summary>
	interface IDreamDaemonClient: IRightsClient<DreamDaemonRights>
	{
		/// <summary>
		/// Get the <see cref="DreamDaemon"/> represented by the <see cref="IDreamDaemonClient"/>
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="DreamDaemon"/> represented by the <see cref="IDreamDaemonClient"/></returns>
		Task<DreamDaemon> Read(CancellationToken cancellationToken);

		/// <summary>
		/// Start <see cref="DreamDaemon"/>
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Start(CancellationToken cancellationToken);

		/// <summary>
		/// Stop <see cref="DreamDaemon"/>
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Stop(CancellationToken cancellationToken);

		/// <summary>
		/// Update <see cref="DreamDaemon"/>. This may trigger <see cref="DreamDaemon.SoftRestart"/>
		/// </summary>
		/// <param name="dreamDaemon">The <see cref="DreamDaemon"/> to update</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Update(DreamDaemon dreamDaemon, CancellationToken cancellationToken);
	}
}
