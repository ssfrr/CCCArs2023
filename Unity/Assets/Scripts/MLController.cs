using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MLController: MonoBehaviour {
	// make our singleton instance
	public static MLController Instance;
	public MLSystem System;

	//private FMOD.System system;
	List<MultiListener> listeners = new();
	List<MLSource> sources = new();
	List<MLPanner> panners = new();

	/*
	*************************
	TODO
	- figure out initialization. make sure that the Awake/Start setup
	  is correct and reliable
	************************
	*/
	public void AddListener(MultiListener l) {
		foreach(MLSource s in sources) {
			panners.Add(new MLPanner(System, s, l));
		}
		listeners.Add(l);
	}

	public void RemoveListener(MultiListener l) {
		foreach(MLPanner p in panners) {
			if(p.Listener == l) {
				panners.Remove(p);
			}
		}
		listeners.Remove(l);
	}

	public void AddSource(MLSource s) {
		foreach(MultiListener l in listeners) {
			panners.Add(new MLPanner(System, s, l));
		}
		sources.Add(s);
	}

	public void RemoveSource(MLSource s) {
		foreach(MLPanner p in panners) {
			if(p.Source == s) {
				panners.Remove(p);
			}
		}
		sources.Remove(s);
	}

	void Awake() {
		if(Instance != null && Instance != this) {
			Debug.LogError("Multiple MultiListenerControllers. There can be only one.");
		}
		Instance = this;
		System = new MLSystem();
	}

	// Start is called before the first frame update
	void Start() {
		//system = FMODUnity.RuntimeManager.CoreSystem;
	}

	// Update is called once per frame
	void Update() {
		System.Update();
	}
}
