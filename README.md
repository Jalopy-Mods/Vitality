# Vitality

[![](https://img.shields.io/github/downloads/Jalopy-Mods/Vitality/total)](#)
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/A0A8OGPIQ)

This mod introduces 5 new worries in your gameplay: fatigue, hunger, thirst, "bathroom needs" and stress. These have minor effects on the gameplay, only affecting sleeping at motels and your vision. They can all be managed by consuming items, sleeping, or using the bathroom in motels.

Vitality supports the **Mobility** mod made by **Max**. It can be downloaded on the [official MinskWorks Discord server](https://discord.gg/TqCwKdR). It affects the rate at which stamina is regenerated and consumed, if enabled.

It also supports the JaLoader Enhanced Movement setting - it adds an optional stamina bar that affects jumping and sprinting.

* Having high fatigue will make your character doze out randomly, and you won't be able to see anything.
* Being drunk from drinking wine will make you character's vision wavy, and he'll also randomly doze out for short periods of time.
* Fatigue is influenced by your hunger and thirst, higher values mean faster fatigue accumulation.
* Stamina is influenced by fatigue, hunger and thirst.

### How to install
1. Make sure you have [JaLoader](https://github.com/theLeaxx/JaLoader) 3.2.0+ installed
### Manual method: Place it in your mods folder (default is `Documents/Jalopy/Mods`)
### Automatic method: 
* **This requires that the `Enable JaDownloader` setting in `Modloader Options/Preferences` in-game is set to `Yes`**
* Open this link in a browser:
* `jaloader://install/jalopy-mods/Vitality`

## For developers

Additionally, Vitality adds a way for custom objects to specify if your object is consumable, and what vitals it affects. 

It's as simple as adding a `VitalityStats` component to your GameObject before registering it.
### This has to be done via reflection, to make sure no errors occour if the user doesn't have Vitality installed.
```csharp
            // Here's an example - if you find a better way to do this, please let me know through Discord (username is leaxx)
            var type = ModLoader.Instance.GetTypeFromMod("Leaxx", "Vitality", "Vitality", "VitalityStats");

            if(type != null)
            {
                var comp = objectExample.AddComponent(type);
                comp.GetType().GetField("AffectsFatigueBy").SetValue(comp, 100);
                comp.GetType().GetField("AffectsHungerBy").SetValue(comp, -75);
                comp.GetType().GetField("AffectsThirstBy").SetValue(comp, -50);
                comp.GetType().GetField("AffectsBathroomBy").SetValue(comp, 25);
                comp.GetType().GetField("AffectsStressBy").SetValue(comp, -5);
                comp.GetType().GetField("AffectsDrunknessBy").SetValue(comp, 20);
            }
```

To configure the values, modify the `AffectsFatigueBy`, `AffectsHungerBy`, `AffectsThirstBy`, `AffectsBathroomBy`, `AffectsStressBy` or `AffectsDrunknessBy` values, with the coresponding values.

For example, to have an object reduce hunger by 20 and increase thirst by 10 when consumed, `AffectsHungerBy` would be set to `-20` and `AffectsThirstBy` to `10`.
