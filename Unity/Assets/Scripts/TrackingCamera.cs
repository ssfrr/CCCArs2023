using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OscCore;
using System.Collections.Concurrent;

public class TrackingCamera : MonoBehaviour
{
    public string Name;
    // Start is called before the first frame update
    void Start()
    {
        TrackingController.Instance.RegisterCamera(this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDestroy()
    {
        TrackingController.Instance.UnregisterCamera(this);
    }
}
