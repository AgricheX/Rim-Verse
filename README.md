# Rimverse

![Rimverse](About/Preview.png)

**[Download Rimverse](https://github.com/AgricheX/Rim-Verse/releases/latest/download/Rimverse.zip)**

Rimverse takes your colony into an entire universe: your own ships, star systems, galaxies, living civilizations, real fleets, space combat, human lineages and fully self-sufficient survival in space.

It uses its own ship system (it does not depend on the Odyssey gravship) and runs on the base game, with optional integration for every DLC and several popular mods.

> **Status:** early version (0.1.1). The content is complete in code but has not been fully tested in game yet. Bug reports with the game log are very welcome.

## Features

### Ships
- Build ships from a bridge, hull floors and walls, and modules: engines, fuel and air tanks, air generators, radiators, batteries, solar panels, cargo bays, sensors, gravity generators and more.
- Four simple meters keep a ship alive: **integrity, air, power and heat**.
- Launch to orbit and land again; the whole ship (buildings, terrain and pawns) moves with you.
- A ship tab on the bridge with mass, thrust, fuel, crew, pilot and dodge, plus alerts for low air, high heat and low integrity.

### Space and travel
- Every game gets a generated home star system with planets, moons, belts and rings, with real orbits, gravity, temperature and atmosphere.
- **Sublight travel** between bodies of a system, **warp jumps** between stars, ancient **gates** and **wormholes** that can lead to other galaxies.
- A whole galaxy and its local group (up to 32 galaxies) generated from the seed, explored through a galaxy map.
- Sensors reveal more about what you scan (unknown, rough size, range, exact values) and fill a star catalog.
- Land on airless bodies, breathable worlds that use the base game biomes, and hostile worlds with thin, toxic or dense air.
- Vacuum suits and helmets with their own air supply.

### Civilizations
- Dozens of living civilizations per galaxy, including the ones from the RimWorld lore (sector Empire, glitterworld federations, urbworld corporations, pirates, mechanoids).
- 94 policies across government, ideology, economy, xeno and logistics, with elections, reforms, coups, civil wars and secession.
- Real fleets travel the galaxy, fight off screen and can arrive at your system as actual raids.
- First contact, diplomacy, orbital traders and physical currencies.

### Combat
- Ship to ship combat with range bands, orders (approach, hold, flee, board), shields, missiles and point defense.
- Ship weapons such as autocannons, lasers, plasma, railguns and missile launchers.
- Boarding actions in both directions, surrender and captured ships.

### Life aboard
- Gravity per pawn: zero-g sickness, bone loss, heavy gravity strain, warp sickness and cabin fever.
- Eight human lineages adapted to different worlds (no Humanoid Alien Races needed).
- Generated alien fauna on breathable worlds and ship food.

### Colonies and endgame
- Found abstract colonies on other worlds, build industries and collect income.
- Endgame crises and several endings, with the game continuing afterwards.

### Scenarios
Shipwreck, Drifting ship, Mining station, Imperial refugees, Glitterworld colony, Star tribe, Cryosleep awakening and Rich explorer, plus extra scenarios when Odyssey or Endless Rooms are active.

### Quality of life
A set of optional quality of life features, each one switched off automatically when the original mod that provides it is active. Everything can be configured in the mod settings.

## Requirements

- RimWorld **1.6** (1.7 listed as supported)
- [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077)
- [Vehicle Framework](https://steamcommunity.com/workshop/filedetails/?id=3014915404)

No DLC is required.

## Optional integrations

Extra content loads automatically when these are active:

| Mod / DLC | What it adds |
| --- | --- |
| Royalty | Naval ranks, imperial permits, psycasts and the Empire tied to the lore civilization |
| Ideology | Space memes, precepts, rituals, roles and styles |
| Biotech | Space genes, xenotypes, mechanoids and an onboard school |
| Anomaly | Space entities, anomalous sites and events aboard ships |
| Odyssey | Gravships can be converted, Odyssey orbit layers are joined to the star system, suit compatibility |
| Combat Extended | Compatibility patches |
| Endless Rooms | Integrated events and the A-sync researcher scenario |
| [NL] Facial Animation, Yayo's Animation | Lineage appearance support |
| Save Our Ship 2 | Rimverse suits protect in SOS2 space |
| Vanilla Gravship Expanded 1 & 2 | Their substructures count as hull and grav engines as maneuvering thrusters |

## Installation

1. Subscribe to Harmony and Vehicle Framework.
2. Download **Rimverse.zip** with the link above.
3. Extract it into `RimWorld/Mods/` so the folder `Rimverse` contains `About/About.xml`.
4. Enable it in the mod list, below Harmony and Vehicle Framework.
5. Start a new game with one of the Rimverse scenarios, or a normal start.

## Languages

English and Brazilian Portuguese.

## Building from source

The C# source is in `Source/Rimverse`. It targets .NET Framework 4.8 and needs reference assemblies from RimWorld, Harmony and Vehicle Framework (not included in this repository):

```
dotnet build Source/Rimverse/Rimverse.csproj -c Release -p:RefsPath=<path to refs>
```

`RefsPath` must contain `rw/` (RimWorld managed DLLs), `h/0Harmony.dll` and `vf/Vehicles.dll` + `vf/SmashTools.dll`. The output goes to `1.6/Assemblies/`.

## Author

Made by me, **Agriche**.
