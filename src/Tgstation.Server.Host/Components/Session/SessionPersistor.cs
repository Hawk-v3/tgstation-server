﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Tgstation.Server.Host.Components.Deployment;
using Tgstation.Server.Host.Database;
using Tgstation.Server.Host.System;

using Z.EntityFramework.Plus;

namespace Tgstation.Server.Host.Components.Session
{
	/// <inheritdoc />
	sealed class SessionPersistor : ISessionPersistor
	{
		/// <summary>
		/// The <see cref="IDatabaseContextFactory"/> for the <see cref="SessionPersistor"/>.
		/// </summary>
		readonly IDatabaseContextFactory databaseContextFactory;

		/// <summary>
		/// The <see cref="IDmbFactory"/> for the <see cref="SessionPersistor"/>.
		/// </summary>
		readonly IDmbFactory dmbFactory;

		/// <summary>
		/// The <see cref="IProcessExecutor"/> for the <see cref="SessionPersistor"/>.
		/// </summary>
		readonly IProcessExecutor processExecutor;

		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="SessionPersistor"/>.
		/// </summary>
		readonly ILogger<SessionPersistor> logger;

		/// <summary>
		/// The <see cref="Api.Models.Instance"/> for the <see cref="SessionPersistor"/>.
		/// </summary>
		readonly Api.Models.Instance metadata;

		/// <summary>
		/// Initializes a new instance of the <see cref="SessionPersistor"/> class.
		/// </summary>
		/// <param name="databaseContextFactory">The value of <see cref="databaseContextFactory"/>.</param>
		/// <param name="dmbFactory">The value of <see cref="dmbFactory"/>.</param>
		/// <param name="processExecutor">The value of <see cref="processExecutor"/>.</param>
		/// <param name="logger">The value of <see cref="logger"/>.</param>
		/// <param name="metadata">The value of <see cref="metadata"/>.</param>
		public SessionPersistor(
			IDatabaseContextFactory databaseContextFactory,
			IDmbFactory dmbFactory,
			IProcessExecutor processExecutor,
			ILogger<SessionPersistor> logger,
			Api.Models.Instance metadata)
		{
			this.databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
			this.dmbFactory = dmbFactory ?? throw new ArgumentNullException(nameof(dmbFactory));
			this.processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
		}

		/// <inheritdoc />
		public Task Save(ReattachInformation reattachInformation, CancellationToken cancellationToken) => databaseContextFactory.UseContext(async (db) =>
		{
			if (reattachInformation == null)
				throw new ArgumentNullException(nameof(reattachInformation));

			logger.LogDebug("Saving reattach information: {info}...", reattachInformation);

			await ClearImpl(db, false, cancellationToken);

			var dbReattachInfo = new Models.ReattachInformation
			{
				AccessIdentifier = reattachInformation.AccessIdentifier,
				CompileJobId = reattachInformation.Dmb.CompileJob.Id.Value,
				Port = reattachInformation.Port,
				ProcessId = reattachInformation.ProcessId,
				RebootState = reattachInformation.RebootState,
				LaunchSecurityLevel = reattachInformation.LaunchSecurityLevel,
				LaunchVisibility = reattachInformation.LaunchVisibility,
			};

			db.ReattachInformations.Add(dbReattachInfo);
			await db.Save(cancellationToken);
		});

		/// <inheritdoc />
		public async Task<ReattachInformation> Load(CancellationToken cancellationToken)
		{
			Models.ReattachInformation result = null;
			TimeSpan? topicTimeout = null;

			async Task KillProcess(Models.ReattachInformation reattachInfo)
			{
				try
				{
					await using var process = processExecutor.GetProcess(reattachInfo.ProcessId);
					if (process != null)
					{
						if (reattachInfo == result)
						{
							logger.LogWarning("Killing PID {pid} associated with CompileJob-less reattach information...", reattachInfo.ProcessId);
						}
						else
						{
							logger.LogWarning("Killing PID {pid} associated with extra reattach information...", reattachInfo.ProcessId);
						}

						process.Terminate();
						await process.Lifetime;
					}
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Failed to kill process!");
				}
			}

			await databaseContextFactory.UseContext(async (db) =>
			{
				var dbReattachInfos = await db
					.ReattachInformations
					.AsQueryable()
					.Where(x => x.CompileJob.Job.Instance.Id == metadata.Id)
					.Include(x => x.CompileJob)
					.ToListAsync(cancellationToken);
				result = dbReattachInfos.FirstOrDefault();
				if (result == default)
					return;

				var timeoutMilliseconds = await db
					.Instances
					.AsQueryable()
					.Where(x => x.Id == metadata.Id)
					.Select(x => x.DreamDaemonSettings.TopicRequestTimeout)
					.FirstOrDefaultAsync(cancellationToken)
					;

				if (timeoutMilliseconds == default)
				{
					logger.LogCritical("Missing TopicRequestTimeout!");
					return;
				}

				topicTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds.Value);

				bool first = true;
				foreach (var reattachInfo in dbReattachInfos)
				{
					if (first)
					{
						first = false;
						continue;
					}

					await KillProcess(reattachInfo);

					db.ReattachInformations.Remove(reattachInfo);
					logger.LogTrace("Deleting ReattachInformation {id}...", reattachInfo.Id);
				}

				await db.Save(cancellationToken);
			});

			if (!topicTimeout.HasValue)
			{
				logger.LogDebug("Reattach information not found!");
				return null;
			}

			var dmb = await dmbFactory.FromCompileJob(result.CompileJob, cancellationToken);
			if (dmb == null)
			{
				logger.LogError("Unable to reattach! Could not load .dmb!");
				await KillProcess(result);

				await databaseContextFactory.UseContext(async db =>
				{
					logger.LogTrace("Deleting ReattachInformation {id}...", result.Id);
					await db
						.ReattachInformations
						.AsQueryable()
						.Where(x => x.Id == result.Id)
						.DeleteAsync(cancellationToken);
				});
				return null;
			}

			var info = new ReattachInformation(
				result,
				dmb,
				topicTimeout.Value);

			logger.LogDebug("Reattach information loaded: {info}", info);

			return info;
		}

		/// <inheritdoc />
		public Task Clear(CancellationToken cancellationToken) => databaseContextFactory
			.UseContext(
				db =>
				{
					logger.LogDebug("Clearing reattach information");
					return ClearImpl(db, true, cancellationToken);
				});

		/// <summary>
		/// Clear any stored <see cref="ReattachInformation"/>.
		/// </summary>
		/// <param name="databaseContext">The <see cref="IDatabaseContext"/> to use.</param>
		/// <param name="instant">If an SQL DELETE WHERE command should be used rather than an Entity Framework transaction.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
		/// <returns>A <see cref="Task"/> representing the running operation.</returns>
		async Task ClearImpl(IDatabaseContext databaseContext, bool instant, CancellationToken cancellationToken)
		{
			var baseQuery = databaseContext
				.ReattachInformations
				.AsQueryable()
				.Where(x => x.CompileJob.Job.Instance.Id == metadata.Id);

			if (instant)
				await baseQuery
					.DeleteAsync(cancellationToken)
					;
			else
			{
				var results = await baseQuery.ToListAsync(cancellationToken);
				foreach (var result in results)
					databaseContext.ReattachInformations.Remove(result);
			}
		}
	}
}
