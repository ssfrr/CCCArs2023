using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
the multilistener has two main functions:
1. creates a 2-channel destination for audio that sends to the correct pair of
   channels in the multichannel audio interface.
2. Tracks an its location and triggers an event whenever it changes
*/

public class MultiListener : TrackedTransform
{
    public int OutputIndex;
    MLDSP stereoOutput;

    // Start is called before the first frame update
    void Start() {
        MLSystem system = MLController.Instance.System;
        MLDSP masterFader = system.GetMasterChannelGroup().GetDSP(0);
        // a `MIXER` doesn't do any processing, it's just for applying the mix matrix
        stereoOutput = system.CreateDSPByType(FMOD.DSP_TYPE.MIXER);
        // set the input to stereo
        //stereoOutput.SetChannelFormat(FMOD.CHANNELMASK.STEREO, 2, FMOD.SPEAKERMODE.STEREO);
        stereoOutput.SetChannelFormat(0, 0, FMOD.SPEAKERMODE.STEREO);
        FMOD.DSPConnection conn = masterFader.AddInput(stereoOutput);
        setupOutputMixMatrix(conn);
        MLController.Instance.AddListener(this);
    }

    void setupOutputMixMatrix(FMOD.DSPConnection conn) {
        conn.getMixMatrix(null, out int outChans, out int inChans);
		if(inChans != 2) {
			Debug.LogErrorFormat("Expected 2 input channels but got {0}", inChans);
		}
		if(outChans < 2 * OutputIndex) {
			Debug.LogErrorFormat("Output needs at least {0} channels but has {0}", 2 * OutputIndex, outChans);
		}
		// set up the mix matrix to send this stereo pair to the correct pair
		// of the multichannel output
		float[] mixMatrix = new float[outChans * inChans];
		mixMatrix[4 * OutputIndex] = 1;
		mixMatrix[4 * OutputIndex + 3] = 1;
	    Debug.Log(mixMatrix);
		conn.setMixMatrix(mixMatrix, outChans, inChans);
	}

    public void AddInput(MLDSP dsp) {
        stereoOutput.AddInput(dsp);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        // TODO: tear down DSP nodes
        Debug.LogWarning("Destroyed a MultiListener, but we haven't fully implemented the destructor");
        MLController.Instance.RemoveListener(this);
    }
}
