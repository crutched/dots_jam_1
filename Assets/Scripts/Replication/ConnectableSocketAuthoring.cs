using Unity.Entities;
using UnityEngine;

public class ConnectableSocketAuthoring : MonoBehaviour {

    class Baker : Baker<ConnectableSocketAuthoring> {

        public override void Bake(ConnectableSocketAuthoring authoring) {
            var ent = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ConnectableSocket>(ent);
            AddComponent<BelongsToReplicationTag>(ent);
        }
    }
}
