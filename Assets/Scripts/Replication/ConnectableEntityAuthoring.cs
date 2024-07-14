using Unity.Entities;
using UnityEngine;

public class ConnectableEntityAuthoring : MonoBehaviour {

    class Baker : Baker<ConnectableEntityAuthoring> {

        public override void Bake(ConnectableEntityAuthoring authoring) {
            var ent = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ConnectableEntity>(ent);
        }
    }
}
