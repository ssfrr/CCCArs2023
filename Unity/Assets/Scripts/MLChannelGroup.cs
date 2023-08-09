using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static FMODUtils;

// convenience class to provide a better API
public class MLChannelGroup
{
    public FMOD.ChannelGroup ChannelGroup;

    public MLChannelGroup(FMOD.ChannelGroup group) {
        ChannelGroup = group;
    }

    public MLDSP GetDSP(int idx) {
        CheckResult(ChannelGroup.getDSP(idx, out FMOD.DSP dsp));
        return new MLDSP(dsp);
    }
}
