using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OscCore;
using System.Collections.Concurrent;

public class TrackingController: MonoBehaviour {
	private abstract class TrackingMsg {
		public TrackingCamera Cam;
		public TrackingMarker Marker;
		public abstract void UpdateTracking();
	}
	private class RotMsg: TrackingMsg {
		public Quaternion Rotation;
		public RotMsg(TrackingCamera cam, TrackingMarker marker, Quaternion rot) {
			Cam = cam;
			Marker = marker;
			Rotation = rot;
		}
		public override void UpdateTracking() {
			if(Marker.Type == TrackingMarker.MarkerType.Mobile) {
				// set the marker's rotation relative to the camera's
				Marker.transform.rotation = Cam.transform.rotation * Rotation;
			}
		}
	}
	private class PosMsg: TrackingMsg {
		public Vector3 Position;
		public PosMsg(TrackingCamera cam, TrackingMarker marker, Vector3 pos) {
			Cam = cam;
			Marker = marker;
			Position = pos;
		}
		public override void UpdateTracking() {
			if(Marker.Type == TrackingMarker.MarkerType.Mobile) {
				// set the marker's position relative to the camera's
				Marker.transform.position = Cam.transform.position + Cam.transform.rotation * Position;
			}
		}
	}
	public int ReceivePort = 53330;
	public static TrackingController Instance;
	OscServer osc;
	ConcurrentQueue<TrackingMsg> msgQueue = new();
	List<TrackingCamera> cameras = new();
	List<TrackingMarker> markers = new();
	// called with `String.Format` with camera name and marker ID
	const string oscPosAddr = "/{0}/marker/{1}/position";
	const string oscRotAddr = "/{0}/marker/{1}/rotation";

	void Awake() {
		if(Instance != null && Instance != this) {
			Debug.LogError("Multiple TrackingControllers. There can be only one.");
		}
		Instance = this;
		osc = new OscServer(ReceivePort);
	}

	public void RegisterCamera(TrackingCamera cam) {
		cameras.Add(cam);
		foreach(TrackingMarker marker in markers) {
			registerOscHandlers(cam, marker);
		}
	}

	public void UnregisterCamera(TrackingCamera cam) {
		foreach(TrackingMarker marker in markers) {
			unregisterOscHandlers(cam, marker);
		}
		cameras.Remove(cam);
	}

	public void RegisterMarker(TrackingMarker marker) {
		markers.Add(marker);
		foreach(TrackingCamera cam in cameras) {
			registerOscHandlers(cam, marker);
		}
	}

	public void UnregisterMarker(TrackingMarker marker) {
		foreach(TrackingCamera cam in cameras) {
			unregisterOscHandlers(cam, marker);
		}
		markers.Remove(marker);
	}

	// unity's coordinate system is +X right, +Y up, +Z forwards
	// the apriltags solver coordinate system if +X right +Y down, +Z forwards
	// so we need to do some conversion
	void registerOscHandlers(TrackingCamera cam, TrackingMarker marker) {
		// create a matrix that flips the y axis
		// note that each argument is a column, so the matrix is the transpose
		// of how it looks visually here. However, this happens to be symmetric
		// so it doesn't matter!
		//Matrix4x4 flipY = new Matrix4x4(
		//	new Vector4(1,  0, 0, 0),
		//	new Vector4(0, -1, 0, 0),
		//	new Vector4(0,  0, 1, 0),
		//	new Vector4(0,  0, 0, 1));
		osc.TryAddMethod(string.Format(oscPosAddr, cam.Name, marker.Id),
			msg => {
				Vector3 vec = new();
				// note that because of a bug in OSCCore, we're actually sending 4 values,
				// but the last one is just a dummy value
				for(int i = 0; i < 3; i++) { vec[i] = msg.ReadFloatElement(i); }
				// flip the y axis
				vec[1] = -vec[1];
				msgQueue.Enqueue(new PosMsg(cam, marker, vec));
			});

		osc.TryAddMethod(string.Format(oscRotAddr, cam.Name, marker.Id),
			 msg => {
				 Matrix4x4 mtx = new();
				 for(int i = 0; i < 3; i++) {
					 for(int j = 0; j < 3; j++) {
						 mtx[i, j] = msg.ReadFloatElement(i * 3 + j);
						 // if A is our rotation matrix, we can define a coordinate
						 // system conversion matrix F =[1 0 0; 0 -1 0; 0 0 1] that flips
						 // the y axis. To convert the given rotation matrix we need to
						 // compute FAF to flip, rotate, then flip again. It turns out that
						 // that negates the matrix elements corresponding to odd numbers
						 // in the source array.
						 if((i*3+j) % 2 == 1) { mtx[i, j] *= -1;  }
					 }
				 }
				 // make sure it's a valid rotation/scale/translate matrix
				 mtx[3, 3] = 1;
				 //Debug.LogFormat("got matrix\n{0}", mtx.ToString());
				 msgQueue.Enqueue(new RotMsg(cam, marker, mtx.rotation));
			 });
	}

	void unregisterOscHandlers(TrackingCamera cam, TrackingMarker marker) {
		osc.RemoveAddress(string.Format(oscPosAddr, cam.Name, marker.Id));
		osc.RemoveAddress(string.Format(oscRotAddr, cam.Name, marker.Id));
	}

	// Start is called before the first frame update
	void Start() {
	}

	// Update is called once per frame
	void Update() {
        while (msgQueue.TryDequeue(out TrackingMsg msg))
        {
			msg.UpdateTracking();
        }
    }
}
