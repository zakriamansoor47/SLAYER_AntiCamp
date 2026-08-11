![](https://img.shields.io/github/downloads/zakriamansoor47/SLAYER_AntiCamp/total?style=for-the-badge)

# Accepting Paid Request! Discord: Slayer47#7002
# Donate:
If you like this project, consider supporting me and give the repo a star ⭐ to keep me motivated to continue improving and updating the plugin! You can also support me through donations, which will help me dedicate more time to development and add new features faster. Thank you for your support!:

[![PayPal](https://www.paypalobjects.com/webstatic/mktg/logo/pp_cc_mark_37x23.jpg)](https://paypal.me/zakriamansoor)

## Description:
This Plugin Detect player who is camping for a specific time. Then notify everyone that he is camping at what location. Also, punish the camper with **SLAY** or **SLAP** Punishment. You can also set **Beacon** on the camper. This plugin is ported from [CSGO AntiCamp](https://forums.alliedmods.net/showthread.php?p=895990).  Take cash, Blind players featured are not added in this version.

## Installation:
**1.** Upload files to your server.

**2.** Edit **configs/plugins/SLAYER_AntiCamp/SLAYER_AntiCamp.json** if you want to change the settings.

**3.** Change the Map **or** Restart the Server **or** Load the Plugin.

## Configuration:
```
{
	"PluginEnabled": true,		// Enable/Disable Anticamp Plugin (true = Enabled, false = Disabled)
	"CampTime": 15,			// The amount of times a suspected camper is checked for
	"SlapORSlayPunishment": 1,		// Set 1 to slap or 2 to slay (kills instantly). Set 0 to disable both
	"SlapDamage": 5,			// Amount of health decreased while camping. Only For Slap Punishment
	"MinHealthReserve": 15,		// Minimum health a camper reserves. Set 0 to slap till dead.
	"FirstPunishDelay": 2.0,		// Delay between camper notification and first punishment in seconds
	"PunishFrequency": 2.0,		// Time between punishments while camping in seconds
	"CampCheckRadius": 120,		// The radius to check for camping
	"PunishBeacon": true,			// Set Beacon on Camper (true = Enabled, false = Disabled)
	"PunishAnyway": true,			// Set 'false' to allow camping below min health. Set 'true' to punish without damage
	"AllowTCampHostageMap": true,		// Set 'true' to allow camping for Ts on 'cs' maps (Hostage). Set 'false' to disable
	"AllowTCampBombPlanted": true,	// Set 'true' to allow camping for Ts if the bomb is planted. Set 'false' to disable
	"AllowCTCampBombMap": true,		// Set 'true' to allow camping for CTs on 'de' maps (Bomb Map). Set 'false' to disable
	"AllowCtCampBombDropped": true,	// Set 'true' to allow camping for CTs if the bomb dropped. Is only needed if 'AllowCTCampBombMap' is 'false'
	"ConfigVersion": 1
}
```
