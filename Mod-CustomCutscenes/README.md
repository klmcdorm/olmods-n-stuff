### Mod-CustomCutscenes

Harmony-based Overload mod for use with olmod.
Please note that the `-modded` command line option is required to load additional mods in olmod.

#### Custom Cutscene Loader

This mod adds support for custom cutscenes before each level in a singleplayer mission.

You can add cutscenes using an asset bundle named `assets`. This mod will look for a `cutscenes` subfolder next to your `.mission` file; place the OS-specific folders and bundles there. The file structure should look like this:

```
my-campaign.mission
cutscenes/
  linux/
    assets
    assets.manifest
  osx/
    assets
    assets.manifest
  windows/
    assets
    assets.manifest
```

#### Custom Cutscene Assets

For a level named `levelname`, this mod will look for a prefab in the asset bundle named `cutscene_levelname`. 
Note that you will also need to add a `briefing` section to your level's [story text](https://overload.fandom.com/wiki/Story_text_(level_editor)) for cutscenes to appear.

At the moment, ending cutscenes are not supported. Cutscenes before each level only contain one scene. Each "screen" of text will appear over that one scene in sequence. After all the text is gone, the scene ends.

The prefab should contain at least a Camera, and optionally a MeshRenderer named `ui_quad`.

##### Camera Settings

The Camera should definitely include layer 29. It could maybe also include layer 28. It should exclude all other layers (level geometry is _not_ cleared out between levels, so that could otherwise get in the way).

This also means that all objects shown in the cutscene should be on these layers.

##### UI Quad

Cutscene text is rendered to a texture which is then displayed in the cutscene. If you want to match the built-in cutscenes, set the Camera's field of view to 37, then place a Quad relative to the Camera using the following transformations in order:
* Scale by (0.3, 0.3, 0.4)
* Translate by (0, 0, 2)
* Scale by (3, 3, 3)

You don't need to assign a material to this object if you name it `ui_quad`. This mod will search for an object with that name and automatically add the correct material. You can also leave out this object entirely, in which case the mod will try to add one in the location described above.
