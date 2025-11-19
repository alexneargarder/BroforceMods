# Utility Mod

This mod adds a few quality of life features such as being able to instantly go to any level in the campaign, skipping the helicopter section on the world map, speeding up the end of levels, and speeding up the main menu loading. As well as many other things like teleportation, invincibility, etc.

## Installation Instructions

Detailed installation instructions can be found [here](https://steamcommunity.com/sharedfiles/filedetails/?id=2434812447).

## Current Features

![Current Features](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/CurrentFeatures.png)

### General Options

![General Options](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/GeneralOptions.png)

**Skip Intro** - If enabled, skips the intros that play when starting up the game.

**Camera Shake** - If disabled, automatically sets Camera Shake to 0 at the start of every level. For some reason Broforce doesn't like to save your camera shake settings, so this can automatically disable it for you if you find yourself doing that at the start of every campaign.

**Helicopter Skip** - If enabled, doesn't wait for animations and moves the helicopter on the world map screen to the next campaign instantly.

**Enable Ending Skip** - If enabled, speeds up the animations at the end of the level where it shows all the enemies you killed.

**Speed up Main Menu Loading** - If enabled, have the options on the main menu show up immediately without having to wait for the eagle scream.

**Helicopter Wait** - If enabled, makes the helicopter at the end of levels wait for all players to be on board before leaving. It'll still leave if they are already dead or die before reaching it.

**Disable Confirmation Menu** - If enabled, disables the yes / no popup that shows when returning to the map, main menu, or restarting a level.

**Disable All Cutscenes** - If enabled, disables all cutscenes in the game that can be disabled without causing issues.

**Scale UI with Window Width** - If enabled, makes the buttons in this UI wider or smaller depending on how wide you have the UMM window set to.

### Level Controls

![Level Controls](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/LevelControls.png)

**Loop Current Level** - If enabled, restarts the level as soon as you beat it.

**Restart Current Level** - Restarts the level when pressed.

**Unlock All Levels** - Press while on the world map screen to unlock all campaigns.

**Go to Level** - Select a campaign and level number and press go to level to instantly go to a level.

**Go to level on startup** - If enabled, starts you on the level you have selected above as soon as the game is started.

**Keyboard 1** - Choose which controller / keyboard is assigned to the player when using the go to level on startup option.

**Previous/Next Level** - Press previous level or next level to move to the previous or next level in a given campaign. You must be in a level for this to work.

### Cheat Options

![Cheat Options](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/CheatOptions.png)

**Invincibility** - If enabled, makes your character permanently invincible.

**Infinite Lives** - If enabled, gives you infinite lives on every level.

**Infinite Specials** - If enabled, gives your character infinite specials.

**Disable Gravity** - If enabled, prevents your character from falling.

**Flight** - If enabled, gives you the ability to fly around.

**Disable Enemy Spawns** - If enabled, prevents any enemies from spawning. This must be enabled before the level loads for it to work.

**Instant kill all enemies** - If enabled, set every enemy's health to 1, allowing you to one-shot them. This must be enabled before the level loads for it to work.

**Summon Mech** - Press to throw a summon mech grenade.

**Time Slow Factor** - Set the Time Slow Factor to control how slow time is when Slow Time is enabled. Lower means slower. Above 1 will make the game run faster, which can be laggy.

**Slow Time** - If enabled, slows time according to the Time Slow Factor.

**Scene Loading Controls** - I would only recommend modders to use these. Basically you can press get current scene to print the name of the current scene to the log. You can type that scene name or any other one into the text box and press load current scene to load it. Immediately load chosen scene will load that scene when the game starts after a couple seconds. It has to wait a few seconds or else the game just freezes. This is useful for modders as you can load into the game faster than you can through the menus, you'll need to press a button on the controller when you load into a level in order to spawn in though.

### Teleport Options

![Teleport Options](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/TeleportOptions.png)

You can see your current position in X and Y coordinates at the top.

**Teleport** - Enter an X and Y coordinate into the box and press Teleport to teleport there.

**Spawn at Custom Waypoint** - If enabled, spawn at the stored spawn position at the start of levels and when respawning.

**Spawn at Final Checkpoint** - If enabled, spawn at the final checkpoint.

**Save Position** - Press to save the position for the given waypoint.

**Teleport to Waypoint** - Press to teleport to the waypoint.

### Debug Options

![Debug Options](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/DebugOptions.png)

**Print Audio Played** - If enabled, prints the name of each audio clip that plays to the log.

**Suppress Announcer** - If enabled, prevents the countdown at the start of levels.

**Max Cage Spawns** - If enabled, makes every potential rescue cage spawn with a prisoner in it.

**Set Zoom Level** - If enabled, overrides the game's default zoom level.

**Zoom Level Slider** - Move the slider to set the zoom level, 1 is the default zoom.

**Capture Unity Logs** - If enabled, prints the Unity logs to UMM's log.

**Step Size** - Sets the increment that the time control keybinds increase / decrease the game speed.

**Pause/Unpause Game** - Set this keybind and press it to pause the game. This does not use the normal pause menu but instead sets the game speed to 0, to prevent anything from happening.

**Decrease / Increase / Reset Game Speed** - Set these keybinds to control the game speed.

### Right Click Options

![Right Click Options](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/RightClickOptions.png)

**Make Cursor Always Visible** - If enabled, prevents the game from hiding the mouse cursor.

**Enable Right Click Menu** - If enabled, allows you to press right click to access the right click menu.

![Right Click Menu](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/RightClickMenu.png)

**Show Help** - Press this to see a popup menu explaining some of the right click's functionality, this menu may appear behind the mod manager window so you may have to drag the mod manager window out of the way, or close it.

![Right Click Help Menu](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/RightClickHelpMenu.png)

**Hold Duration** - Determines the amount of time you need to hold to open the right click menu. Note the right click menu will open instantly unless you have a quick action set, in which case right clicking will perform the quick action and you'll need to hold the specified duration to actually open the right click menu.

**Show Hold Progress Indicator** - If enabled, shows a circle that fills up to indicate how long you have to hold to open the right click menu.

**Enable Recent Items** - If enabled, shows the last few items you've used in the right click menu at the top for easy access.

**Max Recent Items** - Set show many items can appear in the recent items menu.

**Quick Clone** - If you set this keybind and press it, whatever block or enemy is under your mouse will be copied and right clicking will allow you to place copies of it.

**Paint Mode Type** - If the last action you took was placing an enemy or object, then you can hold shift and right click to paint that enemy or object continuously. This setting allows you to control whether the interval that the painting repeats is based on the amount of time that has passed or the distance that your mouse cursor has moved.

**Enemy Spawn Delay** - Sets the amount of time that must pass between spawning an enemy if you're painting and using the time-based paint mode.

**Block/Doodad Spawn Distance** - Sets the amount of distance your cursor must move between painting a block / enemy if you're using the distance-based paint mode.

**Context Menu Style Options** - Changes the appearance of the right click menu

### Keybindings

![Keybindings](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/Keybindings.png)

**Enter Keybinding Mode** - If you press this, you'll be able to bind most toggles / buttons in Utility Mod's UI to a specific keybinding, in order to trigger that button or toggle without having to have the menu open. Look through the other sections after pressing it to see which options can be bound to keys. Press it again to exit once you've finished setting your keybinds.

### Settings Profiles

![Settings Profiles](https://raw.githubusercontent.com/alexneargarder/BroforceMods/refs/heads/master/Screenshots/Utility%20Mod/SettingsProfiles.png)

Settings profiles allow you to quickly change configurations of Utility Mod, which can be useful in case you'd like to have a profile set up for debugging and another one set up for normal playthroughs.

## Source Code

The source code for this mod along with all my other ones can be found [here](https://github.com/alexneargarder/BroforceMods).

