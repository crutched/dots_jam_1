using UnityEngine;

[DisallowMultipleComponent]
public class CamerasRef : MonoBehaviour {
    public static CamerasRef instance;

    public Camera mainCamera;
    public Camera replicationCamera;

    private void Awake() {
        instance = this;
    }
}
