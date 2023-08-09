using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FMODUtils
{
    public static void CheckResult(FMOD.RESULT res) { 
        if(res != FMOD.RESULT.OK) {
            Debug.LogErrorFormat("FMOD Error: {0}", res);
        }
    }

    public static FMOD.ChannelGroup NullChannelGroup() {
        return new FMOD.ChannelGroup(System.IntPtr.Zero);

    }
}
