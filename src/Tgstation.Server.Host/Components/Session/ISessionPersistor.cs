﻿using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.Host.Components.Session
{
	/// <summary>
	/// Handles saving and loading <see cref="ReattachInformation"/>.
	/// </summary>
	public interface ISessionPersistor
	{
		/// <summary>
		/// Save some <paramref name="reattachInformation"/>.
		/// </summary>
		/// <param name="reattachInformation">The <see cref="ReattachInformation"/> to save.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
		/// <returns>A <see cref="ValueTask"/> representing the running operation.</returns>
		ValueTask Save(ReattachInformation reattachInformation, CancellationToken cancellationToken);

		/// <summary>
		/// Load a saved <see cref="ReattachInformation"/>.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
		/// <returns>A <see cref="ValueTask{TResult}"/> resulting in the stored <see cref="ReattachInformation"/> if any.</returns>
		ValueTask<ReattachInformation> Load(CancellationToken cancellationToken);

		/// <summary>
		/// Clear any stored <see cref="ReattachInformation"/>.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
		/// <returns>A <see cref="ValueTask"/> representing the running operation.</returns>
		ValueTask Clear(CancellationToken cancellationToken);
	}
}
