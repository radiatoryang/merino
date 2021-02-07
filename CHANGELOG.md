# Merino: Changelog

all releases and downloads are at https://github.com/radiatoryang/merino/releases

## v2.0.0, February 2021
- now using Unity Package Manager (UPM) folder structure with package.json
- updated to Yarn Spinner v2.0.0, and for ease of use, now pegging Merino's version numbers to Yarn Spinner
- added "Refresh" button, for when you edit .yarn files externally and need to reload into Yarn Spinner
- added auto refresh, which automatically reloads files from disk when you give Merino keyboard focus (click in a textbox) AND when you have autosave enabled

## v0.6.0, 23 July 2019
- New feature Node Map; you can now create and connect nodes visually, and even visualize links between nodes across different files… thanks Addie Lombardo @charblar for doing all the foundation / most of the work on this feature
- New feature PlaytestScope, which lets you playtest with all nodes from “All Files” loaded into Merino, or load only nodes in the “Same File”, or playtest just the single “Node Only”... this is good for when you have a bunch of files with standardized node names (e.g. every NPC in your game has a “Start” node) that would conflict with each other if you tried to play them all at once
- Improved playtest error messages to try to guess filenames better
- Fixed [issue #24](https://github.com/radiatoryang/merino/issues/24), where play mode unloads files from Merino (but only under certain conditions) because we weren’t marking the ScriptableObject as dirty… and now we are!
- Certain characters (and whitespace) are now "reserved" / forbidden for use in node titles, because these characters cause undefined or strange behaviors in Yarn Spinner. Merino automatically strips these characters when you edit the node title. See https://github.com/thesecretlab/YarnSpinner/issues/168 for more about this.
- Removed giant Yarn Spinner .PSD files from _complete.unitypackage, to shave off 10 MB

## v0.5.5, 12 July 2019
- fixed [issue #31](https://github.com/radiatoryang/merino/issues/31) (null reference error when ``right-click > add node`` on a node with no children)... basically created this bug with v0.5.4, oops sorry my bad

## v0.5.4, 9 July 2019
- fixed [issue #23](https://github.com/radiatoryang/merino/issues/23) (font path in MerinoStyles.cs wasn't getting updated with folder move)
- fixed [issue #28](https://github.com/radiatoryang/merino/issues/28) (Merino lets you delete all nodes from a file but then refuses to load an empty file? that makes no sense, so now Merino will load empty files OK)
- fixed a bug with filenames on MacOS when creating a new .yarn.txt file
- fixed some file handling for when a file gets deleted or moved out of project folder
- creating new nodes now adds the new node to your selection
- fixed icons so that they work again? maybe?
- updated base project to Unity 2019.1.8f1

## v0.5.3, 5 May 2019
- hotfix for Unity 2019.1, where TextArea had wrong font size... thanks for bug report and fix, Richard Pieterse!
- fix for [issue #26](https://github.com/radiatoryang/merino/issues/26), added user configurable line endings... Merino now defaults to `\n` though, to preserve compatibility with Yarn Editor
- merged in standalone playtesting editor window from develop branch... thanks, Addie!
- merged in basic word counter
- merged in experimental spaces-as-tabs tab replacement support [issue #20](https://github.com/radiatoryang/merino/issues/20)... since tabs currently have a width of 16 (!!!) and I can't actually change that behavior, there's this terrible hack; enable it in Merino preferences (in Unity: `Edit > Preferences > Merino`)

## v0.5.2, 7 March 2019
- hotfix for [issue #25](https://github.com/radiatoryang/merino/issues/25) file loading issues on MacOS

## v0.5.1, 23 January 2019
- hotfix for issue #21 -- moved currentFiles and fileToNodeID and TreeViewState to the ScriptableObject (so that it survives window reloads?), and also have Merino Init() try searching for ScriptableObject via instance ID (I don't know why that's better, if at all, but might as well try it, I can't reproduce the bug either lol)
- potential workaround for issue #19 -- added option to disable duplicate node title validation in Merino editor prefs... to disable, in Unity Editor go to ``Editor > Preferences > Merino``.

## v0.5, 14 October 2018
- revamped UI (more similar to other Unity tabs)
- added lots of icons
- removed "Add New Node" button in sidebar... replaced with "Add New Node" as context menu item in TreeView, and "Add New Node" button in each node container's bottom bar
- only show NoSort button if a sorting mode has been selected
- mark changed files as dirty, and only reimport files that need re-importing (50% done, doesn't work for OnTreeChanged yet)
- when no files are loaded, added more helpful screen and suggested actions
- **new UX workflow: click LOAD FOLDER / LOAD FILE to load files for editing** (in response to [issue #11](https://github.com/radiatoryang/merino/issues/11) and [issue #3](https://github.com/radiatoryang/merino/issues/3) )
- after some consideration, decided to never show folders in tree view (it clutters everything too much, especially when you have 2+ layers of subfolders... I also didn't want to deal with the user drag-and-dropping files between different folders, that's what the Project tab is for!)
- make file nodes undraggable
- selecting file node = shows data / TextAsset reference
- add "New Yarn File" to Project tab's Create context menu / Asset > Create menu
- deleting file nodes in TreeView = unloading them
- don't let DragAndDrop nodes end without a file parent (or on root node)... see HandleDragAndDrop
- drag and drop triggers autosave (if autosave is enabled!) via OnTreeChanged event
- SAVE AS button is now more of a CREATE NEW FILE button
- NEW NODE button is fixed for multi-file support, adds a new node based on the last node you viewed / edited
- added SanitizeRichText, replaces angle-brackets with look-alike characters in the syntax highlight overlay (fixes issue [issue #15](https://github.com/radiatoryang/merino/issues/15) )

## v0.4, 8 September 2018
- yarn compile errors now caught and visualized in the Merino node edit pane
- when clicking "view node source" button when playtesting, Merino now does its best to scroll to that line in the script (but if it can't match that line text exactly, the scroll will fail)
- added node hierarchy / parenting support (can save and load)... NOTE: this is, for now, a Merino-only feature, and is unsupported / probably stripped out if you try to save the same file in another Yarn editor... see issue #3 (see https://github.com/radiatoryang/merino/issues/3) 
- misc UI tweaks (added bottom status bar with "playtest last edited node" button)
- thanks to @charblar for improved node deletion confirmation + simple context menu (https://github.com/radiatoryang/merino/pull/8)

## v0.3.4.1, 31 August 2018
- unexpanded node children weren't getting exported, and hierarchy / parenting isn't saved yet anyway... for now, at least the order of your nodes will be preserved (see https://github.com/radiatoryang/merino/issues/3) 

## v0.3.4, 31 August 2018
- syntax colors now editable in Editor Preferences > Merino (Yarn)
- resizable sidebar (+ remembered and saved via EditorPrefs)
- various settings now remembered between sessions (saved via EditorPrefs)
- line numbers (my solution is brilliant and terrible)
- support for long nodes (250+ lines?)
- fixes for issues #4 (TreeData is null after playmode) and #5 (removed unreliable double-click in Project tab support)

## v0.3.3, 28 August 2018
- moved everything into an /Editor/ folder, so now you can actually make builds again!
- new files now use the template at Assets/Merino/Editor/Resources/NewFileTemplate.yarn.txt -- you can save over this or edit it
- refactored and reorganized a bit

## v0.3.2, 27 August 2018
- initial release