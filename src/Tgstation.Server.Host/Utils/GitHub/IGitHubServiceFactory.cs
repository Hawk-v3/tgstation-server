﻿namespace Tgstation.Server.Host.Utils.GitHub
{
	/// <summary>
	/// Factory for <see cref="IGitHubService"/>s.
	/// </summary>
	public interface IGitHubServiceFactory
	{
		/// <summary>
		/// Create a <see cref="IGitHubService"/>.
		/// </summary>
		/// <returns>A new <see cref="IGitHubService"/>.</returns>
		public IGitHubService CreateService();

		/// <summary>
		/// Create an <see cref="IAuthenticatedGitHubService"/>.
		/// </summary>
		/// <param name="accessToken">The access token to use for communication with GitHub.</param>
		/// <returns>A new <see cref="IAuthenticatedGitHubService"/>.</returns>
		public IAuthenticatedGitHubService CreateService(string accessToken);
	}
}
