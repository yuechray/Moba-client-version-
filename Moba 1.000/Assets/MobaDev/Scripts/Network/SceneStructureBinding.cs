using UnityEngine;

namespace MobaDev.Network
{
    /// <summary>
    /// Attach to pre-placed structure GameObjects in the scene.
    /// The structureId must match the server Structure.Id seeded at publish time.
    /// </summary>
    public class SceneStructureBinding : MonoBehaviour
    {
        [Tooltip("Server Structure.Id — see layout table in MapConfig comments")]
        public ulong structureId;
    }
}
