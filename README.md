# QuantumHangar [EOL]

This plugin is now marked as EOL. Its taking too much out of me to maintain and deal with the constant issues or lack of features for their servers. With my full time job now, I just want to sit back and enjoy playing games with my friends. This wasnt a lifetime endeavor and was just a fun thing to make in the moment. I will still accept PR's and maintain on keen updates, but thats about it. If someone does a few PR's, and they are good, I will transfer this repo to them. Just make sure to shoot me a pm about it so we can talk about it.


**This plugin creates a server-side hangar for your grids. (Grid Garage)**  I recently wanted to create a hangar plugin such that players had the ability to buy and sell player-made ships in a server or cross-server environment. For those wanting similar things, look no further for this was a pain-in-the-ass to make and took way too much of my time. I finally got everything working and looking halfway decent. And I'm not even freaking finished. That's right. There is probably more to come... more... **FEATURES**. The cursed word of doom for my time and patience.

For those looking for a detailed walkthrough and an example watch this epic video made by ME:
[Feature Video](https://www.youtube.com/watch?v=Ck0DMqywC-w&feature=youtu.be) As always, dont hit that subscribe button. Because I could care less.

If you would like to donate through with PayPal click [here.]([https://paypal.me/Garrett493?locale.x=en_US](https://paypal.me/Garrett493?locale.x=en_US))

Special thanks to  _LordTylus_  for letting me use his existing code for ray-casting and checking the environment around the player. Made this plugin elegant in its player use.

**Current Features**
-   Hangar Market (Cross-Server)
- - Admin BP screen for Public offers
-   Checks for enemy players in the set distance to prevent players from using hangar as a PVP element
-   Command cooldown timer/config (Transfers with players over linked servers)
-   Setting different hangar limits for Normal and Scripter level players
-   Customizable Directory
-   Easy player commands
-   PCU/Block Limit check on grid paste
-   Finds clear spot for grid on grid load from hangar
-   Auto-Orientation with gravity
- Configs for PCU & BlockCount
- BlockLimiter refrence
- Auto-Hangar & Auto-Sell
- Customizable Pricing for saving, loading, and storing grids
- Subgrid compatible
- Cross-Server ECON

**Planned Future Features**
-   Optimizations & Performance
- Work on the projection in the hangar market block: Autocenter, No transparency, etc
- Move all sell commands to the block itself. (Let players select and sell ship via the block)
- Bids & Offers
- Bug Fixes

**Commands**   _Now with Admin Discord Support! (yeah, yeah I know)_

-   !hangar save (Attempts to save grid you are looking at)
-   !hangar load [Name or ID number] (Attempts to load specified grid)
-   !hangar list (Lists all active ships and their pcu values that are in your hangar)
-  !hangar info [Name or ID number] (Provides info about the grid in your hangar)
- !hangar sell [Name or ID number] [price no commas] [String Description]    
	- -Ex: !hangar sell 4 50000 "This is a good starting ship bla bla bla"
- !hangar removeoffer [Name or ID number] (Removes a ship form the market)
-   !hangarmod save (Saves grid you are looking at to owners hangar. Ignores Checks)
-   !hangarmod save [GridName] (Saves grid you are looking at to owners hangar. Ignores Checks)
-   !hangarmod load [PlayerName] [Grid Name or Number] (Loads grid in from players hangar and ignores limits)
-   !hangarmod list [PlayerName] (Lists grids under specified players' hangar)
-  !hangarmod info [PlayerName] [Grid Name or Number] (Provides info of the grid)

**Tips & Tricks:**  The hangar directory folder can be any folder on the server drive. If you want the plugin to be cross-server you  **_MUST_**  point all the plugins to one folder. For example: "C:\HangarStorage"

Copying hangars is as simple as copying the folders inside the directory. You can look up steamIDS. Different timer settings on different servers do work. For example, on server 1 you can have an hour wait while on server 2 you can have a two-hour wait. (It compares times when you stored the grid)

For the Cross-server market: It currently only works as-is on a Single Dedicated PC/Server. I have written it with netcode in mind so for you crazy people who want to do cross boxes... you can, you just need to PM me so we can discuss. That will require an external application to run on your machine.

Again if you have any questions feel free to pm me or leave issues and requests here
