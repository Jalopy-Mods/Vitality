# Vitality

[![](https://img.shields.io/github/downloads/Jalopy-Mods/Vitality/total)](#)

This mod introduces 4 new worries in your gameplay: fatigue, hunger, thirst and "bathroom needs". These have minor effects on the gameplay, only affecting sleeping at motels and your vision. They can all be managed by consuming items, sleeping, or using the bathroom in motels.

Vitality supports the **Mobility** mod made by **Max**. It can be downloaded on the [official MinskWorks Discord server](https://discord.gg/TqCwKdR). It affects the rate at which stamina is regenerated and consumed, if enabled.

It also supports the JaLoader Enhanced Movement setting - it adds an optional stamina bar that affects jumping and sprinting.


### How to install
1. Make sure you have [JaLoader](https://github.com/theLeaxx/JaLoader) 3.1.0+ installed
### Manual method: Place it in your mods folder (default is `Documents/Jalopy/Mods`)
### Automatic method: 
* **This requires that the `Enable JaDownloader` setting in `Modloader Options/Preferences` in-game is set to `Yes`**
* Open this link in a browser:
* `jaloader://install/jalopy-mods/Vitality`

## For developers

Additionally, Vitality adds a way for custom objects to specify if your object is consumable, and what vitals it affects. 

It's as simple as adding a `VitalityStats` component to your GameObject before registering it.

To configure the values, modify the `AffectsFatigueBy`, `AffectsHungerBy`, `AffectsThirstBy` or `AffectsBathroomBy` values, with the coresponding values.

For example, to have an object reduce hunger by 20 and increase thirst by 10 when consumed, `AffectsHungerBy` would be set to `-20` and `AffectsThirstBy` to `10`.
