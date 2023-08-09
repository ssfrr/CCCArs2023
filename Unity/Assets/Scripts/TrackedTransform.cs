using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// base class that sends an event when the transform changes
public class TrackedTransform : MonoBehaviour
{
    public delegate void TransformChanged();
    public event TransformChanged OnTransformChanged;

    // Update is called once per frame
    void Update()
    {
        if(transform.hasChanged) {
            OnTransformChanged();
            transform.hasChanged = false;
		}
    }
}
