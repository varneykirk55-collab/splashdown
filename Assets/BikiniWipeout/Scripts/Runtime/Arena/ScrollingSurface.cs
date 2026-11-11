using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Cheap animated water: drifts the base texture of a renderer with a gentle wobble. Uses a
    /// property block, so no material instance is created and there is no simulation cost.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    public sealed class ScrollingSurface : MonoBehaviour
    {
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        [Tooltip("How many times the texture repeats across the surface.")]
        [SerializeField] internal Vector2 tiling = new Vector2(40f, 40f);

        [Tooltip("Texture drift in tiles per second.")]
        [SerializeField] internal Vector2 scrollSpeed = new Vector2(0.03f, 0.018f);

        [Tooltip("Size of the side-to-side wobble, in tiles.")]
        [SerializeField, Min(0f)] internal float wobble = 0.05f;

        private Renderer target;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            target = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            float t = Time.time;
            float x = Mathf.Repeat(t * scrollSpeed.x + Mathf.Sin(t * 0.6f) * wobble, 1f);
            float y = Mathf.Repeat(t * scrollSpeed.y + Mathf.Cos(t * 0.47f) * wobble, 1f);
            target.GetPropertyBlock(block);
            block.SetVector(BaseMapSt, new Vector4(tiling.x, tiling.y, x, y));
            target.SetPropertyBlock(block);
        }
    }
}
