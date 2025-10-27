using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GSShopUI
{
    public enum VRInput
    {
        Button01, // Pad_Left on Vive, X or A buttons on Rift.
        Button02, // Pad_Right on Vive, Y or B buttons on Rift.
        Button03, // Menu Button on Vive, Y or B on Rift
        Button04, // Full-pad click on Vive, X or A on the Rift.

        // --------------------------------------------------------- //
        // Vive up/down quads, only used in experimental
        // --------------------------------------------------------- //
        Button05, // Quad_Up on vive, Y and B on Rift
        Button06, // Quad_Down on vive, X and A on Rift
        // --------------------------------------------------------- //

        Directional, // Thumbstick if one exists; otherwise touchpad.
        Trigger,     // Trigger Button on Vive, Primary Trigger Button on Rift.
        Grip,        // Grip Button on Vive, Secondary Trigger on Rift.
        Any,
        Thumbstick, // TODO: standardize spelling: ThumbStick or Thumbstick?
        Touchpad
    }
}