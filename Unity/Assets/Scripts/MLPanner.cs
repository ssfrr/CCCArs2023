using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using static FMODUnity.RuntimeUtils;

public class MLPanner
{
    public MultiListener Listener;
    public MLSource Source;
    MLDSP dsp;

    public MLPanner(MLSystem system, MLSource source, MultiListener listener) {
        this.Source = source;
        this.Listener = listener;

		dsp = system.CreateDSPByType(FMOD.DSP_TYPE.PAN);
        listener.AddInput(dsp);
        source.AddOutput(dsp);
		source.OnTransformChanged += updatePanning;
		listener.OnTransformChanged += updatePanning;
    }

    ~MLPanner() {
		Source.OnTransformChanged -= updatePanning;
		Listener.OnTransformChanged -= updatePanning;
		// TODO: remove from DSP graph
		Debug.LogWarning("Removed panner, but we haven't fully implemented the destructor yet.");
	}

	private void updatePanning()
	{
		Set3DPose(Source.transform.position, Listener.transform.position, Listener.transform.rotation);
	}

    public void Set3DPose(Vector3 srcPos, Vector3 listenerPos, Quaternion listenerRot) {
		// TODO: need to incorporate listener rotation
		FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI data = new FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI {
			numlisteners = 1,
			// position of sound relative to listener(s)
			relative = new FMOD.ATTRIBUTES_3D[] { (srcPos - listenerPos).To3DAttributes() },
			weight = new float[] {1},
			// position of sound in world coordinates
			absolute = srcPos.To3DAttributes()
		};

		int attrSize = Marshal.SizeOf<FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI>();
		byte[] attrBytes = new byte[attrSize];
		// get an unmanaged handle to the bytes data
		GCHandle h = GCHandle.Alloc(attrBytes, GCHandleType.Pinned);
		// copy the struct data into the bytes array
		Marshal.StructureToPtr(data, h.AddrOfPinnedObject(), false);
		h.Free();
		dsp.DSP.setParameterData((int) FMOD.DSP_PAN._3D_POSITION, attrBytes);
		// not sure if the parameter index should be:
	    // FMOD.DSP_PARAMETER_DATA_TYPE.DSP_PARAMETER_DATA_TYPE_3DATTRIBUTES_MULTI (which is -5)
		// I _think_ that either might work, and the latter is supposed to work across different
		// spatializer plugins, rather then being specific to this panner?
	}
}
