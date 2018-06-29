﻿using Byond.TopicSender;
using Microsoft.AspNetCore.Http;
using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.Host.Components
{
	/// <inheritdoc />
	sealed class DreamDaemonControl : IDreamDaemonControl, IInteropConsumer
	{
		/// <summary>
		/// Generic OK response
		/// </summary>
		const string DMResponseOKGeneric = "OK";

		/// <summary>
		/// The server is requesting to know what port to open on
		/// </summary>
		const string DMQueryPortsClosed = "its_dark";

		const string DMParameterAccessIdentifier = "access";
		const string DMParameterCommand = "command";
		const string DMParameterNewPort = "new_port";

		const string DMCommandChangePort = "change_port";

		/// <inheritdoc />
		public bool IsPrimary
		{
			get
			{
				CheckDisposed();
				return reattachInformation.IsPrimary;
			}
		}

		/// <inheritdoc />
		public IDmbProvider Dmb
		{
			get
			{
				CheckDisposed();
				return reattachInformation.Dmb;
			}
		}

		/// <inheritdoc />
		public ushort Port
		{
			get
			{
				CheckDisposed();
				return reattachInformation.Port;
			}
		}

		/// <inheritdoc />
		public DreamDaemonRebootState RebootState
		{
			get
			{
				CheckDisposed();
				return reattachInformation.RebootState;
			}
		}

		/// <summary>
		/// The up to date <see cref="DreamDaemonReattachInformation"/>
		/// </summary>
		readonly DreamDaemonReattachInformation reattachInformation;

		/// <summary>
		/// The <see cref="IByondTopicSender"/> for the <see cref="DreamDaemonControl"/>
		/// </summary>
		readonly IByondTopicSender byondTopicSender;

		/// <summary>
		/// The <see cref="IInteropContext"/> for the <see cref="DreamDaemonControl"/>
		/// </summary>
		readonly IInteropContext interopContext;

		/// <summary>
		/// The <see cref="IDreamDaemonSession"/> for the <see cref="DreamDaemonControl"/>
		/// </summary>
		readonly IDreamDaemonSession session;

		/// <summary>
		/// The <see cref="TaskCompletionSource{TResult}"/> <see cref="SetPortImpl(ushort, CancellationToken)"/> waits on when DreamDaemon currently has it's ports closed
		/// </summary>
		TaskCompletionSource<bool> portAssignmentTcs;
		/// <summary>
		/// The port to assign DreamDaemon when it queries for it
		/// </summary>
		ushort? nextPort;

		/// <summary>
		/// If we know DreamDaemon currently has it's port closed
		/// </summary>
		bool portClosed;
		/// <summary>
		/// If the <see cref="DreamDaemonControl"/> has been disposed
		/// </summary>
		bool disposed;

		/// <summary>
		/// Construct a <see cref="DreamDaemonControl"/>
		/// </summary>
		/// <param name="reattachInformation">The value of <see cref="reattachInformation"/></param>
		/// <param name="session">The value of <see cref="session"/></param>
		/// <param name="byondTopicSender">The value of <see cref="byondTopicSender"/></param>
		/// <param name="interopRegistrar">The <see cref="IInteropRegistrar"/> used to construct <see cref="interopContext"/></param>
		public DreamDaemonControl(DreamDaemonReattachInformation reattachInformation, IDreamDaemonSession session, IByondTopicSender byondTopicSender, IInteropRegistrar interopRegistrar)
		{
			this.reattachInformation = reattachInformation ?? throw new ArgumentNullException(nameof(reattachInformation));
			this.byondTopicSender = byondTopicSender ?? throw new ArgumentNullException(nameof(byondTopicSender));
			this.session = session ?? throw new ArgumentNullException(nameof(session));
			if (interopRegistrar == null)
				throw new ArgumentNullException(nameof(interopRegistrar));

			interopContext = interopRegistrar.Register(reattachInformation.AccessIdentifier, this);

			portClosed = false;
			disposed = false;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			lock (this)
				if (!disposed)
				{
					session.Dispose();
					interopContext.Dispose();
					Dmb?.Dispose(); //will be null when released
					disposed = true;
				}
		}

		/// <inheritdoc />
		public Task<object> HandleInterop(IQueryCollection query, CancellationToken cancellationToken)
		{
			if (query == null)
				throw new ArgumentNullException(nameof(query));
		
			if (query.ContainsKey(DMQueryPortsClosed))
				lock (this)
					if(nextPort.HasValue)
					{
						var newPort = nextPort.Value;
						nextPort = null;
						portAssignmentTcs.SetResult(true);
						portAssignmentTcs = null;
						return Task.FromResult<object>(new { new_port = newPort });
					}
			return Task.FromResult(new object());
		}

		/// <summary>
		/// Throws an <see cref="ObjectDisposedException"/> if <see cref="Dispose"/> has been called
		/// </summary>
		void CheckDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(DreamDaemonControl));
		}

		/// <inheritdoc />
		public DreamDaemonReattachInformation Release()
		{
			CheckDisposed();
			//we still don't want to dispose the dmb yet, even though we're keeping it alive
			var tmpProvider = reattachInformation.Dmb;
			reattachInformation.Dmb = null;
			Dispose();
			Dmb.KeepAlive();
			reattachInformation.Dmb = tmpProvider;
			return reattachInformation;
		}

		/// <inheritdoc />
		public Task<string> SendCommand(string command, CancellationToken cancellationToken) => byondTopicSender.SendTopic(new IPEndPoint(IPAddress.Loopback, reattachInformation.Port), String.Format(CultureInfo.InvariantCulture, "?{0}={1}&{2}={3}", DMParameterAccessIdentifier, reattachInformation.AccessIdentifier, DMParameterCommand, command), cancellationToken);

		async Task<bool> SetPortImpl(ushort port, CancellationToken cancellationToken) => await SendCommand(String.Format(CultureInfo.InvariantCulture, "{0}&{1}={2}", DMCommandChangePort, DMParameterNewPort, port), cancellationToken).ConfigureAwait(false) == DMResponseOKGeneric;

		/// <inheritdoc />
		public async Task<bool> ClosePort(CancellationToken cancellationToken)
		{
			CheckDisposed();
			if (portClosed)
				return true;
			if (await SetPortImpl(0, cancellationToken).ConfigureAwait(false))
			{
				portClosed = true;
				return true;
			}
			return false;
		}

		/// <inheritdoc />
		public async Task<bool> SetPort(ushort port, CancellationToken cancellatonToken)
		{
			CheckDisposed();
			if (portClosed)
			{
				Task toWait;
				lock (this)
				{
					nextPort = port;
					portAssignmentTcs = new TaskCompletionSource<bool>();
					toWait = portAssignmentTcs.Task;
				}
				await toWait.ConfigureAwait(false);
			}

			if (port == 0)
				throw new ArgumentOutOfRangeException(nameof(port), port, "port must not be zero!");
			return await SetPortImpl(port, cancellatonToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public async Task<bool> SetRebootState(DreamDaemonRebootState newRebootState, CancellationToken cancellationToken)
		{
			var oldActive = RebootState != DreamDaemonRebootState.Normal;
			var newActive = RebootState != DreamDaemonRebootState.Normal;
			if (oldActive == newActive)
				return true;
		}
	}
}
