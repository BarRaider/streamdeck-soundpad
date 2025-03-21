# Soundpad Integration for Elgato Stream Deck

Play sounds directly from Soundpad, without needing to configure hotkeys.

**Author's website and contact information:** [https://barraider.com](https://barraider.com)

# New in v1.8
- Plugin will now indicate when connected to a Trial version of SoundPad

# New in v1.7
- Added support for categories 🔥
  - `Play Random Sound` can now be limited to a specific category
  - `Play Sound` can now be limited to a specific category
- Bypassed previous limit of ~1300 sounds and should now support anything  you put in SoundPad

# New in v1.6
- New `PTP (Push-To-Play)` mode in Play Action stops sound when button is released
- New `Play/Pause` Action allows to pause and resume sounds

## Features
- Support playing sounds based on the index in Soundpad UI
- RecordPTT action - Utilizes Soundpad's internal recording function and allows recording sounds into Soundpad
- "Load Playlist" action, allows loading an existing *.spl file
- `Remove Sound` action allows you to remove a sound at a specific index from a playlist. Along with the `Record PTT` you can now use this as a sampler

### Download

* [Download plugin](https://github.com/BarRaider/streamdeck-soundpad/releases/)

## I found a bug, who do I contact?
For support please contact the developer. Contact information is available at https://barraider.com

## I have a feature request, who do I contact?
Please contact the developer. Contact information is available at https://barraider.com

## Dependencies
* Uses StreamDeck-Tools by BarRaider: [![NuGet](https://img.shields.io/nuget/v/streamdeck-tools.svg?style=flat)](https://www.nuget.org/packages/streamdeck-tools)
* Uses [soundpad-connector](https://github.com/medokin/soundpad-connector) by medokin
* Uses [Easy-PI](https://github.com/BarRaider/streamdeck-easypi) by BarRaider - Provides seamless integration with the Stream Deck PI (Property Inspector) 