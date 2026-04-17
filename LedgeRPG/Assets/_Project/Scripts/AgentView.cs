using LedgeRPG.Core.World;
using UnityEngine;

namespace Magi.LedgeRPG
{
    public sealed class AgentView : MonoBehaviour
    {
        public float TileSize = 0.55f;

        private static readonly Color AgentColor = new Color(0.25f, 0.55f, 1.00f);
        private GameObject _body;

        private void Awake()
        {
            _body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _body.name = "AgentBody";
            _body.transform.SetParent(transform, worldPositionStays: false);
            _body.transform.localScale = new Vector3(TileSize * 0.8f, TileSize * 0.6f, TileSize * 0.8f);
            _body.GetComponent<Renderer>().material.color = AgentColor;
            Destroy(_body.GetComponent<Collider>());
        }

        public void SetPosition(HexCoord at)
        {
            var world = HexLayout.ToWorld(at, TileSize);
            transform.position = world + new Vector3(0, TileSize * 0.6f, 0);
        }
    }
}
