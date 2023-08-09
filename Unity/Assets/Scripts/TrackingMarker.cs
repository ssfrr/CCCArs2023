using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackingMarker : MonoBehaviour
{
    // mobile markers are used for tracking objects, fixed markers
    // are used for calibrating the camera position
    public enum MarkerType { Mobile, Fixed }
    public int Id;
    public float SizeCm;
    public MarkerType Type;
    

    // Start is called before the first frame update
    void Start()
    {
        TrackingController.Instance.RegisterMarker(this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDestroy()
    {
        TrackingController.Instance.UnregisterMarker(this);
    }
}
