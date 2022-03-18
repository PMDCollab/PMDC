Important notes

---

Repo Setup
* Run `git submodule update --init --remote -- RogueEssence` to get the RogueEssence submodule.
* Run `git submodule update --init --recursive` to get all the submodules.
* You may need to regenerate NuGet packages for the RogueEssence solution first, before building.
* If you switch to or from on the DotNetCore branch, remember to clear your obj folder.

Building Game
* Run `dotnet publish -c Release -r win-x86 PMDC/PMDC.csproj` to publish to Windows x86.
* Run `dotnet publish -c Release -r win-x64 PMDC/PMDC.csproj` to publish to Windows.
* Run `dotnet publish -c Release -r linux-x64 PMDC/PMDC.csproj` to publish to Linux.
* Run `dotnet publish -c Release -r osx-x64 PMDC/PMDC.csproj` to publish to Mac.
* Files will appear in the `publish` folder.

Building Server
* Run `dotnet publish -c Release -r win-x64 RogueEssence/WaypointServer/WaypointServer.csproj` to publish to Windows.
* Run `dotnet publish -c Release -r linux-x64 RogueEssence/WaypointServer/WaypointServer.csproj` to publish to Linux.

Building Updater
* Run `dotnet publish -c Release -r win-x64 Updater/Updater.csproj` to publish to Windows.
* Run `dotnet publish -c Release -r linux-x64 Updater/Updater.csproj` to publish to Linux.
* Run `dotnet publish -c Release -r osx-x64 Updater/Updater.csproj` to publish to Linux.
* Files will appear in the `publish` folder.