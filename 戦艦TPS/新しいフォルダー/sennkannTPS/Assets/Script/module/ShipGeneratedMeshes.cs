using System.Collections.Generic;
using UnityEngine;

namespace Script
{
    public sealed class ShipGeneratedMeshes : MonoBehaviour
    {
        public readonly List<Mesh> Meshes = new List<Mesh>();
        private void OnDestroy()
        {
            foreach (var mesh in Meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
