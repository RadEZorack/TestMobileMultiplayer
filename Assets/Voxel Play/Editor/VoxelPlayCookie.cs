using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public static class VoxelPlayCookie {

        static string[] cookies = {
                                                "A World contains Biomes and each biome contains different terrain, trees and vegetation voxels.",
                                                "Click 'Expand/Collapse World' to configure Terrain, Water and Sky features.",
                                                "A Biome is defined by a range of altitude and a range of moisture.",
                                                "A Biome can be related to several altitude and moisture ranges.",
                                                "Water flood range can be configured in World Settings.",
                                                "Water can only be destroyed in Build mode.",
                                                "In Play Mode, press F1 to show the console and Tab to show the inventory.",
            "Always measure real Performance in a build, not inside Unity Editor.",
                                                "Increasing 'New Chunk Buffer Size' reserves more chunks at once and can reduce the frecuency of memory spikes.",
                                                "'Render in Editor' is useful to place additional static content in your world.",
                                                "You can create a World, Biome or Voxel type right clicking on your Project panel and select the option under Voxel Play submenu.",
                                                "Setup Fog Distance and Fog Falloff to match camera's far clip and produce a smooth fade in/out of environment.",
                                                "Voxel Play uses 4 main custom components: Environment, FPS Controller, Player and Behaviour.",
                                                "Voxel Play Environment allows you to setup your scene. The world configuration is stored in the World Definition asset.",
                                                "Voxel Play FPS Controller is a customized FPS controller that takes care of stuff like crosshair, controls anf footfalls.",
                                                "Voxel Play Player component stores the player inventory and attributes, like life points.",
                                                "Voxel Play Behaviour is an optional component that dynamically updates voxel lighting on animated models on the scene.",
            "An optional Third Person Controller is included for demonstration purposes.",
                                                "Enter /unstuck to move character on top of ground.",
                                                "You can write your own Terrain Generator and attach it to the World.",
                                                "The user guide is now online on kronnect.com/docs/voxel-play/ - check it out!",
                                                "You can create models using Qubicle and import/convert them into Voxel Play Model Definitions using the tools below.",
                                                "Get cool extensions from our support forum on https://kronnect.com/support.",
                                                "You can edit the world in Play Mode and enter /save filename in the console to save your changes.",
                                                "Custom voxel definitions allow you to use a prefab instead of a regular cube. They are useful for vegetation, trees and objects.",
                                                "The Voxel Play Player component exposes hit and damage properties for bare hands and the current item. Each item definition can define custom hit properties like range, damage or attack speed."
                                };

        public static string GetCookie(int cookieIndex) {
            int c = cookieIndex % cookies.Length;
            return cookies[c];
        }

    }
}