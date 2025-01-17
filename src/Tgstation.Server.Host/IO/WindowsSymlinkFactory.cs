﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using BetterWin32Errors;
using Tgstation.Server.Host.System;

namespace Tgstation.Server.Host.IO
{
	/// <summary>
	/// <see cref="ISymlinkFactory"/> for windows systems.
	/// </summary>
	sealed class WindowsSymlinkFactory : ISymlinkFactory
	{
		/// <inheritdoc />
		public bool SymlinkedDirectoriesAreDeletedAsFiles => false;

		/// <inheritdoc />
		public Task CreateSymbolicLink(string targetPath, string linkPath, CancellationToken cancellationToken) => Task.Factory.StartNew(
			() =>
			{
				ArgumentNullException.ThrowIfNull(targetPath);
				ArgumentNullException.ThrowIfNull(linkPath);

				// check if its not a file
				var flags = File.Exists(targetPath) ? NativeMethods.CreateSymbolicLinkFlags.None : NativeMethods.CreateSymbolicLinkFlags.Directory;

				/*
				 * no don't fucking use this
				 * sure it works in SOME cases
				 * i.e. win10 1803+ and IN DEVELOPER MODE
				 * other times it throws ERROR_INVALID_PARAMETER
				 * but the fucking worst is there are some configurations of windows that accept the argument and allow the function to succeed
				 * BUT IT DOESN'T CREATE THE FUCKING LINK
				 * I AM NOT DEBUGGING THAT SHIT AGAIN AHHH

				flags |= NativeMethods.CreateSymbolicLinkFlags.AllowUnprivilegedCreate;
				*/

				cancellationToken.ThrowIfCancellationRequested();
				if (!NativeMethods.CreateSymbolicLink(linkPath, targetPath, flags))
					throw new Win32Exception();
			},
			cancellationToken,
			DefaultIOManager.BlockingTaskCreationOptions,
			TaskScheduler.Current);
	}
}
