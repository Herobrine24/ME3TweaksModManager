![Documentation Image](images/documentation_moddesc.png)

# moddesc.ini - the definition of an M3 mod
M3 mods are defined as folders in a mod user's mod library. In a user's mod library, there are folders for each supported game: ME1/ME2/ME3/LE1/LE2/LE3/LELauncher. Mods for each respective game go in each of these folders. Inside of these game folders are individual mod folders. In each mod folder, there is a **moddesc.ini file**, along with the modded files and folders that make up the mod. The moddesc.ini file describes the mod and what features of M3 the mod will use for both displaying the mod and its options to the user, as well as how the mod is installed. A moddesc.ini is what makes a mod an M3 compatible mod.

![Mod folder structure](https://i.imgur.com/vAZUjPa.png)

#### Backwards compatibility
M3 is backwards compatible with older builds of ME3Tweaks Mod Manager, as well as mods that were designed to be used with Mass Effect 3 Mod Manager (which was also written by ME3Tweaks). This is done through a system called **moddesc version targeting**. At the top of every moddesc.ini file, there is a section header `[ModManager]`, and underneath it is a descriptor named `cmmver`. The value you assign to cmmver will change how M3 handles your mod. I strive to ensure all mods targeting old versions remain functional.

![cmmver](https://i.imgur.com/nmHxJIs.png)

#### What target do I choose?
Targetting different versions indicates which versions of M3 can use your mod and what features/mod operations you can describe in your moddesc file. For the most part it is best to use the latest version, as the automatic updater in M3 brings most users to the latest version. If you are making simple mods, sometimes using an older moddesc format is easier. I describe the features of each version of moddesc.ini below.

You can edit your moddesc.ini by hand, but the most common way is with the moddesc.ini editor build into Mod Manager, which you can access by right clicking any mod and selecting `Moddesc.ini editor`. For non-DLC mods, you typically will need to clone an existing non-DLC mod and then begin building your mod from that clone.

#### Mod restrictions
M3 mods cannot include .exe, .dll, or .asi files. Mods will always fail validation if these files are found - these files can pose security risks. .asi files can be installed through ASI Mod Manager in the Mod Management menu. Additionally, you cannot deploy files with \_metacmm.txt files in them, as these are generated by Mod Manager on mod install to store data about the mod installation. Do not include them in your mod.

#### ME3Tweaks ModMaker mods
ME3Tweaks ModMaker mods are compiled against the the most recently supported version of moddesc that the compiling Mod Manager version supports, which is typically the base number (e.g. 4.4.1 compiles against moddesc 4.4). ModMaker mods are designed on ME3Tweaks ModMaker, so you won't need to worry about anything ModMaker related when building a moddesc.ini file.

### Using the ME3Tweaks Mod Updater Service
M3 has a mod updating mechanism for mods that allows all users to update their mods (that are registered and serviced on ME3Tweaks) without having to manually have the user download and import the mod. I have surveyed some end users and many say this is one of the best features of M3.

If you want to use this service, you can contact me on the ME3Tweaks Discord and we can discuss using the service for your mod. All mods that are on the updater service must be M3 mods (because it's the only way they can do so), and will be listed on the mods page of ME3Tweaks. Package files in your mod must be decompressed or the updater service will reject them (the files are compressed on upload - native package compression must not be used). The main download does not need to be hosted on ME3Tweaks. This service is free as long as your mod is a reasonable size (and my web host doesn't complain).


### moddesc.ini parser strictness
In M3, the parser for a moddesc.ini file became stricter than it was for ME3CMM. A few examples of things that will cause a mod to fail parsing that worked in ME3CMM, but not M3, include:
 - A line without a `=` on it that is not a `;comment`, that is not a header, or is not blank. The parser in M3 will refuse to parse files that don't follow this format. In ME3CMM, lines that didn't match this were simply ignored, but they had zero functional impact beyond making it appear like what was entered into the file did something, when it actually did nothing.
 - Non-matching `(` and `)` in a list descriptor.  A `)` must always be opened by a `(` unless in a quoted string. An unclosed `(` or an unexpected `)` that has no matching opening `(` will cause the parser to mark the mod as invalid. In ME3CMM, this was a bug where an error was not thrown for this condition.
 - Duplicate descriptor keys. The M3 moddesc.ini parser is set to not allow duplicate descriptor keys and will throw an error if you try to use one. In ME3CMM it just used whatever the latest set value was.

ME3CMM sometimes allowed these mods to still load as the code to parse various items was not as robust as it is in M3.

## Version Targeting
ME3Tweaks Mod Manager is fully backwards compatible with mods targeting older versions of moddesc (that followed the spec), but upgrading a mod's moddesc version without upgrading the contents of the file may not work properly as different moddesc versions are parsed differently. 

M3 will refuse to load mods higher than its listed supported moddesc version. This is not really an issue you should worry about as I typically restrict developers from releasing mods with new moddesc features until the userbase has full access to a supported version of M3 through its built-in updater.

## The 4 main components of moddesc.ini
moddesc.ini files have 4 main components: 

 - The [ModManager] header, which contains the targeting information
 - The [ModInfo] header, which contains display information and information about the mod
 - The [UPDATES] header which contains deployment and updater service information
 - The mod task headers such as [CUSTOMDLC] or [RETALIATION]

![Moddesc](https://i.imgur.com/xCMVcLn.png)

## [ModManager] Header
The [ModManager] header is a required header for all moddesc.ini files. It supports a few descriptors that are used to change how the moddesc.ini parser works.

### [ModManager] Descriptors
| Descriptor | Data type | Purpose                                                                                                                                                                                                                                                                                                                                                                                                                        | Required | Supported versions |
|------------|-----------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|--------------------|
| cmmver     | Float     | This descriptor is set to a specific version to tell Mod Manager how to parse the file, and what features may or may not be used by the parser. You may see this referred to as **moddesc version**. You assign this value to ensure forwards compatibility, in the event I have to change how moddesc parsing works - I will always strive to ensure a version targeting a previous version will remain usable in the future. | Yes      | 2.0+               |
| importedby | Integer   | As a mod developer you should never set this value. This is a compatibility shim for mods imported into M3 before Build 103 to indicate they should force target Mass Effect 3. After Build 103 was created, the 'game' descriptor was forced to always be present, and this flag indicates that it should use ME3 for those mods.                                                                                             | No       | 6.0 (Build 103+)   |
| minbuild   | Integer   | This descriptor is used to specify that a mod is only allowed to load on a specific build. For example, if a mod depends on features only present in Build 104, minbuild can be used to ensure users on Build 103 or lower cannot attempt to load the mod.                                                                                                                                                                     | No       | 6.0 (Build 104+)   |

#### What cmmver should I pick?
You typically will want to simply target the latest version. The moddesc.ini editor built into Mod Manager cannot save against older versions of moddesc, and the majority of users will be on the latest version due to the built in update prompts in Mod Manager.

Valid values for cmmver are listed below with the main highlights of that release:

#### Moddesc features by version
| cmmver Version | Games supported | Release date | Release Highlights                                                                                                                                                   |
|----------------|-----------------|--------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1.0/1.1        | ME3             | 2012         | Basic Coalesced.bin swapping only                                                                                                                                    |
| 2.0            | ME3             | 2013         | Supports modifying SOME of the official game DLC (see headers table below)                                                                                           |
| 3.0            | ME3             | 2014         | Supports modifying the basegame and TESTPATCH DLC                                                                                                                    |
| 3.1/4.0        | ME3             | 2015         | Supports installation of Custom DLC mods                                                                                                                             |
| 4.1            | ME3             | 2015         | Supports adding and removing files from the game                                                                                                                     |
| 4.2            | ME3             | 2016         | Supports the altfiles descriptor for CustomDLC, supports sideloading, blacklisting, and manifest building tags                                                       |
| 4.3            | ME3             | 2016         | Supports marking added files as read only, supports modding balance changes                                                                                          |
| 4.4            | ME3             | 2016         | Supports the altdlc and outdatedcustomdlc descriptors for CustomDLC                                                                                                  |
| 4.5            | ME3             | 2017         | Supports the altfiles descriptor in OFFICIAL headers. I do not recommend targeting this version as OP_SUBSTITUTE behaves differently due to backwards compatibility. |
| 5.0            | ME3             | 2017         | Last 32-bit version. Supports the requireddlc descriptor to require specific DLC(s) to be installed before mod will install                                          |
| 5.1            | ME3             | 2018         | Last ME3CMM version. Supports the additionaldeploymentfolders descriptor to allow inclusion of additional folders that are not specifically installed                |
| 6.0            | ME1/ME2/ME3     | 2019         | First M3 version. Supports customization of alternate auto/not-applicable text, multi-dlc metacmm names, game directory structured automapping of source folders and more                                                                |
| 6.1            | ME1/ME2/ME3             | 2020         | Supports installation of 'localization' mods, which are TLK files for existing mods (ME2/ME3)                                                                                          |
| 6.2            | ME1/ME2/ME3             | 2021         | Supports banner images for the mod and alternate installation options, support for required 'single' dlc (for example, depending on CEM Lite or Full)                           |
| 7.0            | OT / LE             | 2021         | Supports Legendary Edition, merge mods, and Game 1 TLK merge
| 8.0            | OT / LE             | 2022         | Supports alternate dependencies, alternate sorting, flattening output of multilist applications
If a cmmver descriptor is not set, the default 1.0 value will be used, which has almost no features.

## [ModInfo] Header
The [ModInfo] Header is used for the description, version, and other information about the mod that the user will see in Mod Manager. It also houses the ME3Tweaks Updater Service information. 

### [ModInfo] Descriptors
| Descriptor  | Value                                            | Purpose & Notes                                                                                                                                                                                                                                                                                                                                                                                         | Required                                     | Supported Versions |
|-------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------|--------------------|
| modname     | String                                           | Mod name displayed in Mod Manager. This value should not be too long or the user will have to scroll the mod list to read the entire title of your mod.                                                                                                                                                                                                                                                 | Yes                                          | All                |
| game        | String                                           | Game this mod is for. Valid values are ME1, ME2 and ME3. If cmmver is less than 6 and this descriptor is not set, a default value of ME3 is assumed. You should simply always set this value.                                                                                                                                                                                                           | Yes if cmmver >= 6.0, Optional if cmmver < 6 | 6.0+               |
| moddesc     | String                                           | Mod description shown on Mod Manager. Newlines can be inserted by adding `<br>` where you want the newline.                                                                                                                                                                                                                                                                                             | Yes                                          | All                |
| modver      | Semantic version number                                            | Mod version, as shown in Mod Manager. This value is also used to detect updates. Required for mod deployment and use of the updater service. Mods targetting moddesc 6.0 or higher should ensure it is a semantic version number, in one of the following three formats: X.Y, X.Y.Z, or W.X.Y.Z, where WXYZ are numerals. Mods targetting moddesc 5.1 or lower should use the format X.Y only.                                                                                                                                                                                                                                                          | Yes                                          | All                |
| moddev      | String                                           | Mod developer(s). Shown in the mod description panel.                                                                                                                                                                                                                                                                                                                                                   | Yes                                          | All                |
| modcoal     | Integer                                          | Any value other than zero indicates that there is a coalesced swap job for Mod Manager 2.0 mods. A file named Coalesced.bin must be in the same folder as moddesc.ini. This variable only works with moddesc targeting version 2.0. Moddesc 1.0 only does coalesced swap, and Moddesc 3.0 and above is done by adding a [BASEGAME] header with a replacement of /BIOGame/CookedPCConsole/Coalesced.bin. | No                                           | 2.0 only           |
| modsite     | String (URL)                                     | If present, a clickable link anchored at the bottom of the mod description panel will go to this URL. You should put the page that users can go to for support as this is the main reason they will go there. Using a proper nexusmods URL will also enable your mod to check for updates if your mod is whitelisted for update checks. See above for the definition of a proper NexusMods URL.         | No                                           | All                |
| modid       | Integer                                          | ModMaker Mod ID. Should not be manually added to any mods. Value is shown in the mod description panel and used for checking for updates.                                                                                                                                                                                                                                                               | No                                           | All                |
|updatecode|Integer|ME3Tweaks Updater Service update code. This is used to get the manifest from ME3Tweaks for classic mods. If you don't have an update code assigned from ME3Tweaks, don't use this descriptor.|No|6.0+|
| nexuscode   | Integer                                          | Allows you to define your NexusMods mod ID specifically. If you are using a proper NexusMods URL as your modsite, this value is already set. This is for mods that do not use a NexusMods URL as their modsite (such as ME3Tweaks mods). If you are using a proper NexusMods URL or don't have a modsite value set, this value is ignored.                                                              | No                                           | 6.0+               |
| requireddlc | Unquoted Semicolon Separated List (DLC folder names) | Specifies a list of DLC folders that must exist in order for this mod to allow installation. For example, Spectre Expansion Mod requires Expanded Galaxy Mod, so it sets this value to `DLC_MOD_EGM`. If the mod also required MEHEM, it would be `DLC_MOD_EGM;DLC_CON_MEHEM`.                                                                                                                                                                                                    | No                                           | 5.0+               |
| prefercompressed | Boolean | Indicates if the mod should automatically tick the 'Compress packages' checkbox in the mod importer window. Package compression will slow down extraction but will save disk space for the end user. This setting only applies to mods for ME2 and ME3, and the value is ignored if the mod uses the ME3Tweaks Updater Service, as all Updater Service mods files must be uncompressed so the hash checks work properly.                                                                                                                                                                                                   | No                                           | 6.1+, OT mods only               |
| bannerimagename | String (Filename) | Indicates the filename of the file located under M3Images to use as the banner. See [Mod images](modimages.md) for information on how to add images to your mod that will be shown at install time.                                                                                                                                                                                                | No                                           | 6.2+               |
| nexusupdatecheck | Boolean | Setting this value to False will disable the ability to check for updates for this mod. This can be used when you have more than one file on a NexusMods mod page, in order to stay compliant with the NexusMods Update Check service rules. If this value is not specified, the default value is True.                                                                                                                                            | No                                           | 7.0+               |

## [UPDATES] Header
The UPDATES header is used when deploying your mod as well as information about how it is stored on the ME3Tweaks Updater service, if you choose to use it.

### [UDPATES] Descriptors
|Descriptor|Value|Purpose & Notes|Required|Supported Versions|
|--- |--- |--- |--- |--- |
|serverfolder|Unquoted String|Path to your server storage area. Typically this is 3rdparty/<username>/modname. This is only used when generating the manifest.|Required for Updater Service, Not used for Deployment|All|
|blacklistedfiles|Unquoted Semicolon Separated List (String)|Relative file paths here from the mod's root directory will be deleted upon update. This is used to delete old files that may have fallen out of the scope of the mod folders. For example, I used to ship a .cmd file with SP Controller Support, which I blacklisted to ensure it was deleted on update so it would no longer be used.|Optional for Updater Service, Not used for Deployment|4.2+|
|additionaldeploymentfolders|Unquoted Semicolon Separated List (String)|Folders specified here will be considered part of your mod, but are not part of its installation. For example, a folder containing headmorphs can be included in your mod, in both deployments and the updater service. Note you can only specify top level folders with this descriptor, not files or subdirectories.|Optional for Updater Service, Optional for Deployment|5.1+|
|additionaldeploymentfiles|Unquoted Semicolon Separated List (String)|Root-level files that should be included in your server update or included in your archives when deploying your mod. Only root level files are supported.|Optional for Updater Service, Optional for Deployment|6.0+|