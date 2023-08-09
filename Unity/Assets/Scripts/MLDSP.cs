using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static FMODUtils;

// convenience class to provide a better API
public class MLDSP {
    public FMOD.DSP DSP;

    public MLDSP(FMOD.DSP dsp) {
        DSP = dsp;
    }

    public FMOD.DSPConnection AddInput(MLDSP dsp) {
        CheckResult(DSP.addInput(dsp.DSP, out FMOD.DSPConnection conn));
        return conn;
    }

    public void SetChannelFormat(
            FMOD.CHANNELMASK mask, int nChannels, FMOD.SPEAKERMODE mode) {
        CheckResult(DSP.setChannelFormat(mask, nChannels, mode));
    }
}
