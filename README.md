# merino
<img width=50% align=right src=https://raw.githubusercontent.com/radiatoryang/merino/master/merino05_demo.gif> 

Merino is a narrative design tool that lets you write Yarn scripts inside the Unity Editor, built on top of [Yarn Spinner](https://github.com/thesecretlab/YarnSpinner) and [Yarn](https://github.com/InfiniteAmmoInc/Yarn) (a [Twine](http://twinery.org/)-like script language)

**NEWS, 21 Dec 2021: with the full release of Yarn Spinner 2.0, I've decided to archive this repo. The editing experience in VS Code has become much better: [the official YS extension](https://github.com/YarnSpinnerTool/VSCodeExtension) has node map and basic syntax highlighting + there's a fantastic [YS language server](https://github.com/pappleby/YarnSpinnerLanguageServer) that gives you autocomplete and interfaces between Unity C# and Yarn scripts automagically. Thanks to everyone who supported this project! You should now use the VS Code tools instead haha**

**NEWS, 23 Feb 2020: Merino is currently NOT compatible with recent updates to Yarn Spinner (v1.0+)... I'll update it after Yarn Spinner finalizes a lot of stuff, but in the meantime, it doesn't make sense to try to hit a moving target... sorry!** ... see https://github.com/radiatoryang/merino/issues/39

## download / install
download and install from the [Releases page](https://github.com/radiatoryang/merino/releases);
- "complete" .unitypackage includes Yarn Spinner, "minimal" .unitypackage is just Merino folder

## usage / help / how-to / documentation
- [documentation](https://github.com/radiatoryang/merino/wiki), troubleshooting, how to write stories and dialogue with Yarn
- [changelog](https://github.com/radiatoryang/merino/wiki/Changelog)

### roadmap
- edit Yarn node tags
- line tagging for localization (pending new Yarn Spinner update)
- playtest logs / replays
- port writing interface to new Unity UIElements (Summer 2020)

## maintainers / core contributors
- Robert Yang @radiatoryang
- Adrienne Lombardo @charblar

### acknowledgments
- Unity Editor Coroutines https://github.com/marijnz/unity-editor-coroutines
- Yarn Spinner https://github.com/thesecretlab/YarnSpinner
- Yarn https://github.com/InfiniteAmmoInc/Yarn

### see also
- Ropework, a visual novel framework for Unity / Yarn Spinner https://github.com/radiatoryang/ropework

### license?
MIT
