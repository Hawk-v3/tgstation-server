﻿using System;
using System.Linq;

using Tgstation.Server.Api.Models.Internal;

namespace Tgstation.Server.Api.Models
{
	/// <summary>
	/// <see cref="ChatConnectionStringBuilder"/> for <see cref="ChatProvider.Discord"/>.
	/// </summary>
	public sealed class DiscordConnectionStringBuilder : ChatConnectionStringBuilder
	{
		/// <inheritdoc />
		public override bool Valid => !String.IsNullOrEmpty(BotToken);

		/// <summary>
		/// The Discord bot token.
		/// </summary>
		/// <remarks>See https://discordapp.com/developers/docs/topics/oauth2#bots</remarks>
		public string? BotToken { get; set; }

		/// <summary>
		/// <see cref="bool"/> to enable based mode. Will auto reply with a youtube link to a video that says "based on the hardware that's installed in it" to anyone saying 'based on what?' case-insensitive.
		/// </summary>
		[Obsolete("Will be removed in next major TGS version")]
		public bool BasedMeme { get; set; }

		/// <summary>
		/// If the tgstation-server logo is shown in deployment embeds.
		/// </summary>
		public bool DeploymentBranding { get; set; }

		/// <summary>
		/// The <see cref="DiscordDMOutputDisplayType"/>.
		/// </summary>
		public DiscordDMOutputDisplayType DMOutputDisplay { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="DiscordConnectionStringBuilder"/> class.
		/// </summary>
		public DiscordConnectionStringBuilder()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DiscordConnectionStringBuilder"/> class.
		/// </summary>
		/// <param name="connectionString">The connection string.</param>
		public DiscordConnectionStringBuilder(string connectionString)
		{
			if (connectionString == null)
				throw new ArgumentNullException(nameof(connectionString));

			var splits = connectionString.Split(';');

			BotToken = splits.First();

			if (splits.Length < 2 || !Enum.TryParse<DiscordDMOutputDisplayType>(splits[1], out var dMOutputDisplayType))
				dMOutputDisplayType = DiscordDMOutputDisplayType.Always;
			DMOutputDisplay = dMOutputDisplayType;

			if (splits.Length > 2 && Int32.TryParse(splits[2], out Int32 basedMeme))
#pragma warning disable CS0618 // Type or member is obsolete
				BasedMeme = Convert.ToBoolean(basedMeme);
			else
				BasedMeme = false; // oranges said this needs to be true by default :pensive:

			if (splits.Length > 3 && Int32.TryParse(splits[3], out Int32 branding))
				DeploymentBranding = Convert.ToBoolean(branding);
			else
				DeploymentBranding = true; // previous default behaviour
		}

		/// <inheritdoc />
		public override string ToString() => $"{BotToken};{(int)DMOutputDisplay};{Convert.ToInt32(BasedMeme)};{Convert.ToInt32(DeploymentBranding)}";
#pragma warning restore CS0618 // Type or member is obsolete
	}
}
