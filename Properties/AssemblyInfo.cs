using System.Reflection;
using System.Runtime.InteropServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

[assembly: AssemblyTitle("Stratum UI")]
[assembly: AssemblyDescription("Stratum server/client user interface mod")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("imtsubaki (Tsu) and Stratum")]
[assembly: AssemblyProduct("Stratum")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("f9e5a7c7-4f6e-44d9-b695-15d12b8e1a88")]
[assembly: AssemblyVersion("1.0.7.0")]
[assembly: AssemblyFileVersion("1.0.7.0")]

[assembly: ModInfo("Stratum UI", "stratumui",
    Version = "1.0.7",
    NetworkVersion = GameVersion.NetworkVersion,
    Side = "Universal",
    RequiredOnClient = false,
    RequiredOnServer = false,
    Description = "Stratum UI is a mod made for Stratum powered servers, but will work fully with vanilla servers and singleplayer. It adds a online player list, as well as command autocompletion and a few other quality of life features.",
    Authors = new[] { "Tsu (imtsubaki)" })]