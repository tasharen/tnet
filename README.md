TNet (Tasharen Networking) started off as a networking solution for the Unity Engine. I created it back in 2012 because the game I was working on needed a solid net code foundation.
At the time, Unity's solution at the time was severely lacking and Photon was only getting started (and its CCU limit approach did not sound appealing).
Not long after initial release I realized that the way I approach networking made TNet an excellent tool for game state persistence, making it possible to add save game feature without any extra effort.
The handy DataNode class in particular, offers a hierarchical approach to data similar to XML, but in a much easier to read text format, and offers serialization in plain text, binary and LZMA-compressed data formats.
Nowadays, I use TNet in all of my own projects for both multiplayer and serialization, and as such it's still actively tweaked, yet remains fully backwards compatible with projects created 12+ years ago.

As of this writing (October 2025), TNet is now free and open source.

-Michael "Aren" Lyashenko

discord.gg/tasharen
