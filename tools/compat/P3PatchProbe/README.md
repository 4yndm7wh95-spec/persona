# P3Makoto runtime patch compatibility probe

Checks the actual game assemblies without launching the game: selects the innermost damage overload, extracts targets from the version-specific argument type, chooses the completed merchant room initializer and installs all five affected Harmony patch classes. Tested against 0.111 and 0.113.

Build P3Makoto for the chosen GameDir first, then:

```powershell
dotnet build tools/compat/P3PatchProbe/Probe.csproj -t:Rebuild '-p:GameData=<game>/data_sts2_windows_x86_64' '-p:ModDll=<matching P3Makoto.dll>'
dotnet tools/compat/P3PatchProbe/bin/Debug/net9.0/Probe.dll '<game>/data_sts2_windows_x86_64' 113
```

Use `111` for the legacy damage/merchant path. Execute with .NET 9: the game's Harmony build does not support running this probe under .NET 10. Match the referenced game files to the tested game version. This does not test rendering, audio playback or live shop interaction.
