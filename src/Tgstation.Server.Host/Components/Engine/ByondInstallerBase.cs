﻿using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Internal;
using Tgstation.Server.Host.IO;
using Tgstation.Server.Host.Utils;

namespace Tgstation.Server.Host.Components.Engine
{
	/// <summary>
	/// Base implementation of <see cref="IEngineInstaller"/> for <see cref="EngineType.Byond"/>.
	/// </summary>
	abstract class ByondInstallerBase : EngineInstallerBase
	{
		/// <summary>
		/// The path to the BYOND bin folder.
		/// </summary>
		protected const string BinPath = "byond/bin";

		/// <summary>
		/// The name of BYOND's cache directory.
		/// </summary>
		const string CacheDirectoryName = "cache";

		/// <summary>
		/// The path to the cfg directory.
		/// </summary>
		const string CfgDirectoryName = "cfg";

		/// <summary>
		/// The name of the list of trusted .dmb files in the user's BYOND cfg directory.
		/// </summary>
		const string TrustedDmbFileName = "trusted.txt";

		/// <summary>
		/// The first <see cref="Version"/> of BYOND that supports the '-map-threads' parameter on DreamDaemon.
		/// </summary>
		static readonly Version MapThreadsVersion = new (515, 1609);

		/// <summary>
		/// <see cref="SemaphoreSlim"/> for writing to files in the user's BYOND directory.
		/// </summary>
		static readonly SemaphoreSlim UserFilesSemaphore = new (1);

		/// <inheritdoc />
		protected override EngineType TargetEngineType => EngineType.Byond;

		/// <summary>
		/// Bath to the system user's local BYOND folder.
		/// </summary>
		protected abstract string PathToUserFolder { get; }

		/// <summary>
		/// Path to the DreamMaker executable.
		/// </summary>
		protected abstract string DreamMakerName { get; }

		/// <summary>
		/// Gets the URL formatter string for downloading a byond version of {0:Major} {1:Minor}.
		/// </summary>
		protected abstract string ByondRevisionsUrlTemplate { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ByondInstallerBase"/> class.
		/// </summary>
		/// <param name="ioManager">The <see cref="IIOManager"/> for the <see cref="EngineInstallerBase"/>.</param>
		/// <param name="fileDownloader">The <see cref="IFileDownloader"/> for the <see cref="EngineInstallerBase"/>.</param>
		/// <param name="logger">The <see cref="ILogger"/> for the <see cref="EngineInstallerBase"/>.</param>
		protected ByondInstallerBase(IIOManager ioManager, IFileDownloader fileDownloader, ILogger<ByondInstallerBase> logger)
			: base(ioManager, fileDownloader, logger)
		{
		}

		/// <inheritdoc />
		public override IEngineInstallation CreateInstallation(ByondVersion version, Task installationTask)
		{
			CheckVersionValidity(version);

			var binPathForVersion = IOManager.ConcatPath(version.ToString(), BinPath);
			var supportsMapThreads = version.Version >= MapThreadsVersion;

			return new ByondInstallation(
				installationTask,
				version,
				IOManager.ResolvePath(
					IOManager.ConcatPath(
						binPathForVersion,
						GetDreamDaemonName(
							version.Version,
							out var supportsCli))),
				IOManager.ResolvePath(
					IOManager.ConcatPath(
						binPathForVersion,
						DreamMakerName)),
				supportsCli,
				supportsMapThreads);
		}

		/// <inheritdoc />
		public override async Task CleanCache(CancellationToken cancellationToken)
		{
			try
			{
				var byondDir = PathToUserFolder;

				Logger.LogDebug("Cleaning BYOND cache...");
				var cacheCleanTask = IOManager.DeleteDirectory(
					IOManager.ConcatPath(
						byondDir,
						CacheDirectoryName),
					cancellationToken);

				// Create local cfg directory in case it doesn't exist
				var localCfgDirectory = IOManager.ConcatPath(
					byondDir,
					CfgDirectoryName);

				var cfgCreateTask = IOManager.CreateDirectory(
					localCfgDirectory,
					cancellationToken);

				// Delete trusted.txt so it doesn't grow too large
				var trustedFilePath =
					IOManager.ConcatPath(
						localCfgDirectory,
						TrustedDmbFileName);

				Logger.LogTrace("Deleting trusted .dmbs file {trustedFilePath}", trustedFilePath);
				var trustedDmbDeleteTask = IOManager.DeleteFile(
					trustedFilePath,
					cancellationToken);

				await Task.WhenAll(cacheCleanTask, cfgCreateTask, trustedDmbDeleteTask);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Logger.LogWarning(ex, "Error cleaning BYOND cache!");
			}
		}

		/// <inheritdoc />
		public override async ValueTask TrustDmbPath(string fullDmbPath, CancellationToken cancellationToken)
		{
			var byondDir = PathToUserFolder;
			if (String.IsNullOrWhiteSpace(byondDir))
			{
				Logger.LogTrace("No relevant user BYOND directory to install a \"{fileName}\" in", TrustedDmbFileName);
				return;
			}

			var trustedFilePath = IOManager.ConcatPath(
				byondDir,
				CfgDirectoryName,
				TrustedDmbFileName);

			Logger.LogDebug("Adding .dmb ({dmbPath}) to {trustedFilePath}", fullDmbPath, trustedFilePath);

			using (await SemaphoreSlimContext.Lock(UserFilesSemaphore, cancellationToken))
			{
				string trustedFileText;
				if (await IOManager.FileExists(trustedFilePath, cancellationToken))
				{
					var trustedFileBytes = await IOManager.ReadAllBytes(trustedFilePath, cancellationToken);
					trustedFileText = Encoding.UTF8.GetString(trustedFileBytes);
					trustedFileText = $"{trustedFileText.Trim()}{Environment.NewLine}";
				}
				else
					trustedFileText = String.Empty;

				if (trustedFileText.Contains(fullDmbPath, StringComparison.Ordinal))
					return;

				trustedFileText = $"{trustedFileText}{fullDmbPath}{Environment.NewLine}";

				var newTrustedFileBytes = Encoding.UTF8.GetBytes(trustedFileText);
				await IOManager.WriteAllBytes(trustedFilePath, newTrustedFileBytes, cancellationToken);
			}
		}

		/// <inheritdoc />
		protected override ValueTask<Uri> GetDownloadZipUrl(ByondVersion version, CancellationToken cancellationToken)
		{
			CheckVersionValidity(version);
			var url = String.Format(CultureInfo.InvariantCulture, ByondRevisionsUrlTemplate, version.Version.Major, version.Version.Minor);
			return ValueTask.FromResult(new Uri(url));
		}

		/// <summary>
		/// Get the file name of the DreamDaemon executable.
		/// </summary>
		/// <param name="byondVersion">The <see cref="Version"/> of BYOND to select the executable name for.</param>
		/// <param name="supportsCli">Whether or not the returned path supports being run as a command-line application.</param>
		/// <returns>The file name of the DreamDaemon executable.</returns>
		protected abstract string GetDreamDaemonName(Version byondVersion, out bool supportsCli);
	}
}