using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine;

public class UIRef : MonoBehaviour {
    public static UIRef instance;
    public UIDocument dishUI;
    public UIDocument globalUI;
    public UIDocument replicationUI;
    public UIDocument finalUI;
    public Volume postprocessing;

    public void Awake() {
        instance = this;
    }
}
