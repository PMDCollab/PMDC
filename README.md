Important notes

---


*Run `git submodule update --init --remote -- RogueEssence` to get the RogueEssence submodule.
*Run `git submodule update --init --remote -- RawAsset` to get the RawAsset submodule (65535 files!!).
*Run `git submodule update --init --recursive` to get all the submodules.
*You may need to regenerate NuGet packages for the RogueEssence solution first, before building.
*If you switch to or from on the DotNetCore branch, remember to clear your obj folder.

Building Game
*Run `dotnet publish -c Release -r win-x64 PMDO/PMDO.csproj` to publish to Windows.
*Run `dotnet publish -c Release -r linux-x64 PMDO/PMDO.csproj` to publish to Linux.
*Run `dotnet publish -c Release -r osx-x64 PMDO/PMDO.csproj` to publish to Mac.
*Files will appear in the `publish` folder.

Building Server
*Run `dotnet publish -c Release -r win-x64 RogueEssence/WaypointServer/WaypointServer.csproj` to publish to Windows.
*Run `dotnet publish -c Release -r linux-x64 RogueEssence/WaypointServer/WaypointServer.csproj` to publish to Linux.

Building Updater
*Run `dotnet publish -c Release -r win-x64 Updater/Updater.csproj` to publish to Windows.
*Run `dotnet publish -c Release -r linux-x64 Updater/Updater.csproj` to publish to Linux.
*Run `dotnet publish -c Release -r osx-x64 Updater/Updater.csproj` to publish to Linux.

*Files will appear in the `publish` folder.

DataGenerator Deployment Order
*One-time: Run `-itemprep` to generate monster/status/element tables needed for items.
*Run `Scripts/item_sync.py` to update exclusive item spreadsheet with data generated above. It will generate a csv of exclusive items to be used in the `-dump` step.

*Reserialize Skills and Monster (Or regenerate Monster) using `-reserialize Skill` or `-reserialize Monster`
*Dump all data using `-dump`.  It depends on the csv of exclusive items to generate that exclusive items (item creation). It also generates an XML to map species to family items (spawning lookup), and a common_gen.lua containing tables of generic trades and specific trades.

*Generate tables for string merge with `-strings out`.
*Sync the translation table using `Scripts/strings_sync.py`
*Uptake tables for string merge with `-strings in`.