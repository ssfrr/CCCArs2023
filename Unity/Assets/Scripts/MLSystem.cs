using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static FMODUtils;

public class MLSystem
{
    public int MaxChannels = 512;
    public int OutputChannels = 16;

    public FMOD.System System;

    public MLSystem()
    {
        CheckResult(FMOD.Factory.System_Create(out System));
        CheckResult(System.setSoftwareFormat(48000, FMOD.SPEAKERMODE.RAW, OutputChannels));
        // leaving buffer settings alone for now, but we could change them to tweak latency
        CheckResult(System.init(
	        MaxChannels,
	        FMOD.INITFLAGS.NORMAL | FMOD.INITFLAGS.PROFILE_ENABLE | FMOD.INITFLAGS.PROFILE_METER_ALL,
	        (System.IntPtr)null));
        setupDriver();
        Debug.Log("FMOD System Initialized");
    }

    public FMOD.Sound CreateSound(string name, FMOD.MODE mode) {
        CheckResult(System.createSound(name, mode, out FMOD.Sound s));
        return s;
    }

    public FMOD.Channel PlaySound(FMOD.Sound sound) {
        CheckResult(System.playSound(
	        sound, NullChannelGroup(), true, out FMOD.Channel channel));
        return channel;
    }

    public MLChannelGroup GetMasterChannelGroup() { 
        CheckResult(System.getMasterChannelGroup(out FMOD.ChannelGroup master));
        return new MLChannelGroup(master);
    }

    public int GetNumDrivers() {
        CheckResult(System.getNumDrivers(out int nDrivers));
        return nDrivers;
    }

    public MLDSP CreateDSPByType(FMOD.DSP_TYPE type) {
        CheckResult(System.createDSPByType(type, out FMOD.DSP dsp));
        dsp.setActive(true);
        return new MLDSP(dsp);
    }

    // Update should be called once per frame
    public void Update()
    {
        System.update();
    }

    ~MLSystem()
    {
        System.release();
    }

    void setupDriver() {
        int nDrivers = GetNumDrivers();
        Debug.LogFormat("Got {0} Audio Drivers", nDrivers);
        for(int i = 0; i < nDrivers; ++i) {
            logDriverInfo(i);
	    }
        int selectedDriver = selectDriver();
        if(selectedDriver < 0) {
            Debug.LogError("No supported audio device found");
            return;
	    }
        Debug.LogFormat("Using Audio Driver: {0}", getDriverName(selectedDriver));
        System.setDriver(selectedDriver);
    }

    int selectDriver() {
        int i = findDriverByName("24Ao");
        if (i >= 0) return i;
        i = findDriverByName("UltraLite");
        if (i >= 0) return i;
        i = findDriverByName("Speakers"); // TEMPORARY
        if (i >= 0) return i;
        return findDriverByMinChannels(16);
    }

    int findDriverByName(string pattern) { 
        for(int i = 0; i < GetNumDrivers(); ++i) {
            if (getDriverName(i).Contains(pattern)) return i;
	    }
        return -1;
    }

    int findDriverByMinChannels(int minChannnels) { 
        for(int i = 0; i < GetNumDrivers(); ++i) {
            if (getDriverChannels(i) >= minChannnels) return i;
	    }
        return -1;
    }

    string getDriverName(int id) { 
        string name;
        CheckResult(System.getDriverInfo(id, out name, 1024, out _, out _, out _, out _));
        return name;
    }

    int getDriverChannels(int id) { 
        int nChannels;
        CheckResult(System.getDriverInfo(id, out _, 0, out _, out _, out _, out nChannels));
        return nChannels;
    }

    void logDriverInfo(int id) {
        System.Guid guid;
        int rate;
        FMOD.SPEAKERMODE mode;
        int nChannels;
        string name;
        CheckResult(System.getDriverInfo(id, out name, 1024, out guid, out rate, out mode, out nChannels));
        Debug.LogFormat("name: {0}\nGUID: {1}\nnChannels: {2}\nmode: {3}\n sample rate: {4}",
            name, guid, nChannels, mode, rate);
    }
}
