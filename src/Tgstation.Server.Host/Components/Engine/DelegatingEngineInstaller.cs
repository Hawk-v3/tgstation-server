﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Internal;
using Tgstation.Server.Common.Extensions;
using Tgstation.Server.Host.Jobs;

namespace Tgstation.Server.Host.Components.Engine
{
	/// <summary>
	/// Implementation of <see cref="IEngineInstaller"/> that forwards calls to different <see cref="IEngineInstaller"/> based on their appropriate <see cref="EngineType"/>.
	/// </summary>
	sealed class DelegatingEngineInstaller : IEngineInstaller
	{
		/// <summary>
		/// The <see cref="IReadOnlyDictionary{TKey, TValue}"/> mapping <see cref="EngineType"/>s to their appropriate <see cref="IEngineInstaller"/>.
		/// </summary>
		readonly IReadOnlyDictionary<EngineType, IEngineInstaller> delegatedInstallers;

		/// <summary>
		/// Initializes a new instance of the <see cref="DelegatingEngineInstaller"/> class.
		/// </summary>
		/// <param name="delegatedInstallers">The value of <see cref="delegatedInstallers"/>.</param>
		public DelegatingEngineInstaller(IReadOnlyDictionary<EngineType, IEngineInstaller> delegatedInstallers)
		{
			this.delegatedInstallers = delegatedInstallers ?? throw new ArgumentNullException(nameof(delegatedInstallers));
		}

		/// <inheritdoc />
		public Task CleanCache(CancellationToken cancellationToken)
			=> Task.WhenAll(delegatedInstallers.Values.Select(installer => installer.CleanCache(cancellationToken)));

		/// <inheritdoc />
		public IEngineInstallation CreateInstallation(ByondVersion version, Task installationTask)
			=> DelegateCall(version, installer => installer.CreateInstallation(version, installationTask));

		/// <inheritdoc />
		public ValueTask<IEngineInstallationData> DownloadVersion(ByondVersion version, JobProgressReporter jobProgressReporter, CancellationToken cancellationToken)
			=> DelegateCall(version, installer => installer.DownloadVersion(version, jobProgressReporter, cancellationToken));

		/// <inheritdoc />
		public ValueTask Install(ByondVersion version, string path, CancellationToken cancellationToken)
			=> DelegateCall(version, installer => installer.Install(version, path, cancellationToken));

		/// <inheritdoc />
		public ValueTask TrustDmbPath(string fullDmbPath, CancellationToken cancellationToken)
			=> ValueTaskExtensions.WhenAll(delegatedInstallers.Values.Select(installer => installer.TrustDmbPath(fullDmbPath, cancellationToken)));

		/// <inheritdoc />
		public ValueTask UpgradeInstallation(ByondVersion version, string path, CancellationToken cancellationToken)
			=> DelegateCall(version, installer => installer.UpgradeInstallation(version, path, cancellationToken));

		/// <summary>
		/// Delegate a given <paramref name="call"/> to its appropriate <see cref="IEngineInstaller"/>.
		/// </summary>
		/// <typeparam name="TReturn">The return <see cref="Type"/> of the call.</typeparam>
		/// <param name="version">The <see cref="ByondVersion"/> used to perform delegate selection.</param>
		/// <param name="call">The <see cref="Func{T, TResult}"/> that will be called with the correct <see cref="IEngineInstaller"/> based on <paramref name="version"/>.</param>
		/// <returns>The <typeparamref name="TReturn"/> value of the delegated call.</returns>
		TReturn DelegateCall<TReturn>(ByondVersion version, Func<IEngineInstaller, TReturn> call)
		{
			ArgumentNullException.ThrowIfNull(version);
			return call(delegatedInstallers[version.Engine.Value]);
		}
	}
}