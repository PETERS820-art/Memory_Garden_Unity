# Memory Garden Unity

Unity XR prototype project for `Memory Garden`.

## Requirements

- Unity Hub
- Unity `2022.3.62f3c1`
- Git
- Git LFS

## Clone And Open

Use Git LFS when cloning this repository. Some project assets such as textures, audio, models, and HDRI files are stored with LFS.

```bash
git lfs install
git clone https://github.com/PETERS820-art/Memory_Garden_Unity.git
cd Memory_Garden_Unity
git lfs pull
```

Then open the project folder in Unity Hub with Unity `2022.3.62f3c1`.

## What Is Included

This repository includes the files needed to open and work on the project:

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `.meta` files

## What Is Not Included

These folders and files are intentionally ignored because Unity or local tools regenerate them:

- `Library/`
- `Temp/`
- `Obj/`
- `Build/`
- `Builds/`
- `Logs/`
- `UserSettings/`
- `.vs/`
- `.vscode/`
- `.idea/`
- `.vsconfig`
- `*.csproj`
- `*.sln`

If these are missing after clone, that is expected.

## First Open Notes

- The first project open may take a while because Unity will rebuild `Library/`.
- If large assets look missing, make sure Git LFS is installed and run `git lfs pull`.
- This project uses URP, OpenXR, and XR Interaction Toolkit.

## Package Note

The project depends on this Git package:

- `cn.unity.uos.launcher`: `https://cnb.cool/unity/uos/UOSLauncher.git`

If Unity reports package download errors on first open, check whether this URL is reachable from your network.

## Collaboration Tip

Before starting work:

```bash
git pull
git lfs pull
```

Before pushing changes:

```bash
git status
git add .
git commit -m "Your message"
git push
```
