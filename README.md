# KSP2 Redux mod API

The public modding layer of KSP2 Redux, in one repository:

| Folder | What it is | Previously |
|---|---|---|
| `SpaceWarp/` | Mod loader and modding API | [SpaceWarpDev/SpaceWarp](https://github.com/SpaceWarpDev/SpaceWarp), branch `dev` |
| `UitkForKsp2/` | UI Toolkit integration for game and mod UI | [UitkForKsp2/UitkForKsp2](https://github.com/UitkForKsp2/UitkForKsp2), branch `dev` |
| `PatchManager/` | Data patching with Lua and JSON | [KSP2Community/PatchManager](https://github.com/KSP2Community/PatchManager), branch `dev` |
| `V-SwiFT/` | Part variant switching | [KSP2Community/V-SwiFT](https://github.com/KSP2Community/V-SwiFT), branch `dev` |
| `DebugTools/` | In-game debug windows | [KSP2Community/DebugTools](https://github.com/KSP2Community/DebugTools), branch `main` |

Each folder keeps its full history from the original repository, and its tags with a folder prefix (`spacewarp/`, `uitkforksp2/`, `patchmanager/`, `vswift/`, `debugtools/`). Each folder also keeps its own license file.

This code is compiled only inside the KSP2 Redux game project, where this repository is a submodule. Mod projects reference it through the game's assemblies. The versions in the original repositories that target the stock game stay where they are.
