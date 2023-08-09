using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static FMODUtils;

public class MLSource : TrackedTransform
{
    public string Filename = "chris_mann.wav";
    MLController controller;
    MLSystem system;
    FMOD.Channel channel;
    public FMOD.Sound Sound;
    MLDSP output;

    //public FMODUnity.EventReference PlaybackEvent;
    //private FMOD.Studio.EventInstance playbackInstance;

    public void SetPaused(bool paused) {
        channel.setPaused(paused);
    }

    public void AddOutput(MLDSP dsp) {
        dsp.AddInput(output);
    }

	void Awake()
	{
		
	}

	// Start is called before the first frame update
	void Start()
    {
        controller = MLController.Instance;
        system = controller.System;

        Sound = system.CreateSound(
            Application.streamingAssetsPath + "/" + Filename,
            FMOD.MODE.LOOP_NORMAL);

        channel = system.PlaySound(Sound);
        // from here on out we'll work in terms of DSPs
        CheckResult(channel.getDSP(0, out FMOD.DSP dsp));
        output = new MLDSP(dsp);

        controller.AddSource(this);
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnDestroy()
    {
        controller.RemoveSource(this);
    }
}
