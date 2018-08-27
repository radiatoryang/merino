# merino
<img width=50% align=right src=https://raw.githubusercontent.com/radiatoryang/merino/master/merino_example.png> 

Merino is a narrative design tool that lets you write Yarn scripts inside the Unity Editor, built on top of [Yarn Spinner](https://github.com/thesecretlab/YarnSpinner) and [Yarn](https://github.com/InfiniteAmmoInc/Yarn) (a scripting language inspired by [Twine](http://twinery.org/).)

## download / install
download and install from the [Releases page](https://github.com/radiatoryang/merino/releases);
- "complete" .unitypackage includes Yarn Spinner
- "minimal" .unitypackage is just Merino folder

## usage / help / how-to / documentation
read the [wiki documentation](https://github.com/radiatoryang/merino/wiki) for info on writing with Yarn / troubleshooting and tech support

### roadmap
v0.4
- line numbers, zoom to line number
- catch Yarn.Loader parse exceptions, display in Merino window
- refactor code and cleanup
- user preferences: let user customize syntax highlighting colors, fonts / font sizes

v0.5
- auto-complete typing
- detect characters, functions, nodes, variables for auto-complete + /Resources/Editor/MerinoAutocompleteList.txt ?
- inline syntax highlighting for variable names somehow

v0.6
- node map visualization

### uses other peoples code
- Unity Editor Coroutines https://github.com/marijnz/unity-editor-coroutines
- Yarn Spinner https://github.com/thesecretlab/YarnSpinner
- Yarn https://github.com/InfiniteAmmoInc/Yarn

### see also
- Ropework, a visual novel template for Unity / Yarn Spinner https://github.com/radiatoryang/ropework

### license?
MIT
