# ChildToNPC
This is a Stardew Valley mod called Child To NPC. Child To NPC is a modding tool which converts children into NPCs, which allows them to be patched by Content Patcher mods in the sames ways that normal NPCs can be. Please let me know if you run into any issues using it!

## For Players
As a player, there aren't many things you need to do with the main Child To NPC mod. This mod will simply sit in your mod folder and interact with whatever Content Patcher pack you downloaded, which will do most of the work in modifying your children.

The main thing you may want to change about Child To NPC is the config settings.

### Config Options

Child To NPC generates a config.json the first time the game is run. The default config.json looks like this.
```cs
{
  "AgeWhenKidsAreModified": 83,
  "CurfewTime": 2100,
  "ChildParentPairs": {},
  "ModdingCommands": false,
  "ModdingDisable": false
}
```
The field `AgeWhenKidsAreModified` determines the age (in days) when your child is replaced by an NPC. By default, this is set to 83, which is 28 days (one season) after they become a toddler.

The field `CurfewTime` determines the time of day when, after returning home, children will automatically go to bed for the night. This doesn't actually affect their schedule, i.e. they can still follow their schedule after their curfew. It just changes their behavior after they arrive home.

The field `ChildParentPairs` allows you to customize the parentage of your children. Normally, it's assumed that the parent of a child is your current spouse, but if you'd like to have your child customized based on a previous spouse after divorce (or whatever reason you have), you can enter their parentage here.

For example, if want your first child Violet to have Shane as their parent, but your second child Lily to have Elliot as their parent, you would change the `ChildParentPairs` field to look like this.
```cs
  "ChildParentPairs": { 
    "Violet": "Shane",
    "Lily": "Elliot"
  },
```

As a player, you can ignore the `ModdingCommands` and `ModdingDisable` fields and leave them as false. These fields are for use by modders for testing, so you don't need to change them.

## For Modders: Making Content Patcher Packs with Child To NPC

The main purposes of Child To NPC is to make Content Patcher packs possible, so making a mod with Child To NPC will be very similar to making any NPC Content Patcher mod. I highly recommend you look over the links below for instructions on how to make Content Patcher packs editing NPCs. I'd also recommend looking over some of the Custom NPC mods that are already out there.

The Content Patcher Github page has both a [general mod author guide](https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide.md) as well as a specific guide for [using tokens](https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-tokens-guide.md). That second link will be important for making a mod using Child To NPC, specifically the section about [using mod-provided tokens](https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-tokens-guide.md#mod-provided-tokens).

The Stardew Valley Wiki has a page about NPC data [here](https://stardewvalleywiki.com/Modding:NPC_data). **A word of warning:** Child NPCs aren't the same as normal NPCs. In fact, they are most similar to a married NPC. So when you look at NPC data, you will want to look on the parts which are used by married NPCs. For example, the "marriage_job" entries in a marriageable NPC's schedule, etc. There will be more information on this below.

MissCoriel's template for making Content Patcher NPC mods is [here](https://www.nexusmods.com/stardewvalley/mods/3446).

Once you feel comfortable with making Content Patcher mods for NPCs, then you're ready to create a mod with Child To NPC!

(Also, remember to download Spacechase0's Custom NPC Fixes to avoid schedule issues for NPCs, the link to the NexusMods page is [here](https://www.nexusmods.com/stardewvalley/mods/3849).)

### Config Options
As a modder, you may want to use the `ModdingCommands` option. When `ModdingCommands` is set to true, the Child To NPC mod will add console commands to the game which make generating new children easier, for the sake of testing out your CP pack more quickly.

Side note: Changes to the config file aren't implemented until the game is next loaded, so if the config has generated for the first time, you will need to close the game and re-open it before console commands are added.

### Custom Tokens for Content Patcher
Inside the manifest.json, be sure that you list "Loe2run.ChildToNPC" as a required dependency. This will allow you to make use of the Content Patcher tokens that this mod provides.

### Tokens
Child To NPC makes use of the Content Patcher API to create custom tokens. These tokens take the form of:
```cs
"{{Loe2run.ChildToNPC/<Token Name Here>}}"
```
While all the examples below will be using the "First" prefix, which indicates the first child born, these tokens are also available for up to four children. For example, while `FirstChildName` will give you the display name of the first child, there's also `SecondChildName`, `ThirdChildName`, and `FourthChildName`. This extends to all tokens.

#### The basic token: Child
```cs
"{{Loe2run.ChildToNPC/FirstChild}}"
```
This is the token you will use the most. Wherever you would use an NPC's name to Target a specific file, you will instead put this token.

Here's an example entry from a content.json:
```cs
{
  "LogName": "Child Portraits",
  "Action": "Load",
  "Target": "Portraits/{{Loe2run.ChildToNPC/FirstChild}}",
  "FromFile": "assets/FirstChildPortrait.png"
},
```

#### Name
```cs
"{{Loe2run.ChildToNPC/FirstChildName}}"
```
The difference between this token and the previous one is that this token gives you the display name of the NPC. Whenever you want to refer to a character by name, like in dialogue, you will want to use this token.

#### Birthday
```cs
"{{Loe2run.ChildToNPC/FirstChildBirthday}}"
```
This token returns the child's birthday in the form of `day season year`. 

For example, you can use this value when creating the NPC Disposition.

```cs
{
  "LogName": "Child NPC Dispositions",
  "Action": "EditData",
  "Target": "Data/NPCDispositions",
  "Entries": {
    "{{Loe2run.ChildToNPC/FirstChild}}": ".../{{Loe2run.ChildToNPC/FirstChildBirthday}}/..."
  }
},
```

#### Gender
```cs
"{{Loe2run.ChildToNPC/FirstChildGender}}"
```
This token returns the child's gender in the form of the string "male" or "female". This can also be used for the NPC Disposition, or you could use it as a conditional.

Here's an example where is a patch is applied if the child has a specific gender.
```cs
{
    "LogName": "Child Portraits",
    "Action": "Load",
    "Target": "Portraits/{{Loe2run.ChildToNPC/FirstChild}}",
    "FromFile": "assets/FirstSonPortrait.png",
    "When": {
        "{{Loe2run.ChildToNPC/FirstChildGender}}": "male"
    }
},
```

#### Bed Location
```cs
"{{Loe2run.ChildToNPC/FirstChildBed}}"
```
This token by returns a tile position in the form of "x y" where the child will go to bed. It's generated in the same way that Family Planning generates bed spots, so that if there are more than two children, they will share beds. 

#### Parent
```cs
"{{Loe2run.ChildToNPC/FirstChildParent}}"
```
By default, this token will return the name of the current spouse of the player. However, if the Child To NPC config.json has an entry for this child, it will instead return the custom value from there. 

You could, for example, use this to customize the appearance of your child by their birth parent after a divorce has occurred.

```cs
{
  "LogName": "Child Sprites",
  "Action": "Load",
  "Target": "Characters/{{Loe2run.ChildToNPC/FirstChild}}",
  "FromFile": "assets/sprites_{{Loe2run.ChildToNPC/FirstChildParent}}.png"
},
```

#### Total Number of Children
```cs
"{{Loe2run.ChildToNPC/NumberTotalChildren}}"
```
This token is indepdent of any particular child. It just tells you how many children the family has, including children under the age cutoff. Typically this will return a value of "0", "1", or "2". However, if a player is using the Family Planning mod, it may return a higher number. However, Child To NPC currently only provides tokens for up to four children, so you won't be able to modify more than 4 children currently.

## How to Uninstall
To uninstall ChildToNPC, all you have to do is remove the ChildToNPC mod (and any associated Content Patcher mods) from your mod folder. NPC shouldn't be saved within save data, so your children will automatically go back to normal the next time you load the save. (If you do run into a bug which corrupts your save data, please let me know!)

## Final Notes
This mod uses Harmony, so it could run into issues with other mods which patch the same methods. If you notice any issues, let me know!

This mod is currently only compatible with singleplayer.

If you have questions about anything, feel free to get in contact with me! One of the best ways to talk to me is through the Stardew Valley Discord. I'm Loe#4013 there. [Here](https://stardewvalleywiki.com/Modding:Community#Discord) is a link to the Stardew Valley Wiki page about the Discord channel.
