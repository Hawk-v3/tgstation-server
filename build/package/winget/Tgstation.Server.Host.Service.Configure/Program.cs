﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Threading;

using var process = new Process();

var installDir = Path.GetDirectoryName(
	Assembly.GetExecutingAssembly().Location)!;

process.StartInfo.WorkingDirectory = installDir;
process.StartInfo.FileName = Path.Combine(
	installDir,
	"Tgstation.Server.Host.Service.exe");

/*
https://www.roelvanlisdonk.nl/2009/11/13/how-to-detect-unattended-installation-msiexec-quit-or-qn-in-youre-custom-action-with-c/
msiUILevelNoChange 		0 		Does not change UI level.
msiUILevelDefault 		1 		Uses default UI level.
msiUILevelNone 			2 		Silent installation.
msiUILevelBasic 		3 		Simple progress and error handling.
msiUILevelReduced 		4 		Authored UI and wizard dialog boxes suppressed.
msiUILevelFull 			5 		Authored UI with wizards, progress, and errors.
msiUILevelHideCancel 	32 		If combined with the msiUILevelBasic value, the installer shows progress dialog boxes but does not display a Cancel button on the dialog box to prevent users from canceling the installation.
msiUILevelProgressOnly 	64 		If combined with the msiUILevelBasic value, the installer displays progress dialog boxes but does not display any modal dialog boxes or error dialog boxes.
msiUILevelEndDialog 	128 	If combined with any above value, the installer displays a modal dialog box at the end of a successful installation or if there has been an error. No dialog box is displayed if the user cancels.
*/
var uiLevel = Int32.Parse(args[0]);

// leave it to MS to overcomplicate things
var interactive = uiLevel switch
{
	0 or 1 or 4 or 5 => true,
	_ => false,
};

var silent = uiLevel == 2;

var uninstall = args
	.Any(arg => arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));

var shortcut = uiLevel == 42069;

bool startService;
if (shortcut)
{
	Console.WriteLine();
	Console.WriteLine("This program will configure tgstation-server. Would you like to restart the service after doing so?");
	Console.Write("A restart is required to apply changes. It will not kill your DreamDaemon instances provided you are running a version >=5.13.0 (Y/n): ");
	var keyInfo = Console.ReadKey();
	startService = keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.Y;
	Console.WriteLine();
}
else
	startService = interactive;

process.StartInfo.Arguments = uninstall
	? "-u"
	: $"-i -f -x{(startService ? " -r" : String.Empty)}{((interactive || shortcut) ? " -c" : String.Empty)}{(silent ? " -s" : String.Empty)}";

process.Start();

await process.WaitForExitAsync(CancellationToken.None); // DCT: None available

foreach (var installLogFile in Directory.EnumerateFiles(installDir, "*.log"))
	File.Delete(installLogFile);

if (uninstall)
	Directory.Delete(
		Path.Combine(installDir, "lib"),
		true);

if ((shortcut || (interactive && !uninstall)) && process.ExitCode == 0)
{
	// try and lockdown the appsettings file
	var fileInfo = new FileInfo(Path.Combine(installDir, "appsettings.Production.yml"));

	//get security access
	FileSecurity fs = fileInfo.GetAccessControl();

	//remove inherited access
	fs.SetAccessRuleProtection(true, false);

	// Explicitly grant admins and SYSTEM
	fs.AddAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
	fs.AddAccessRule(new FileSystemAccessRule("NT AUTHORITY\\SYSTEM", FileSystemRights.Read, AccessControlType.Allow));

	fileInfo.SetAccessControl(fs);
}

if (shortcut)
{
	Console.WriteLine("Press any key to exit this program...");
	Console.ReadKey();
	Console.WriteLine();
}

// returning non-zero can make the installation uninstallable, no thanks
return uninstall ? 0 : process.ExitCode;
