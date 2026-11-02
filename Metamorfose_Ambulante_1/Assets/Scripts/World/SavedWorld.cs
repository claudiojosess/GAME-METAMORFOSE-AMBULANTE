using System.Collections.Generic;
using UnityEngine;
namespace AeroMorphET
{
    // O cenário inicial já está salvo na cena. Este script apenas prolonga a corrida.
    public sealed class SavedWorld : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject[] initialSegments;
        [SerializeField] private GameObject[] templates;
        [SerializeField] private float segmentLength = 32f;
        [SerializeField] private Transform mountains;
        [SerializeField] private float behindDistance = 48f;
        [SerializeField] private Terrain[] terrainTiles;
        private readonly Queue<GameObject> active = new();
        private float nextZ;
        private Vector3 mountainOrigin;
        private Terrain[] repeatedTerrainTiles;
        private float terrainLength;
        private float originalTerrainStart;
        private float repeatedTerrainStart;
        private void Awake()
        {
            var ordered = new List<GameObject>();
            foreach (var chunk in initialSegments) if (chunk != null) ordered.Add(chunk);
            ordered.Sort((a,b) => a.transform.position.z.CompareTo(b.transform.position.z));
            foreach (var chunk in ordered)
            {
                active.Enqueue(chunk);
                nextZ = Mathf.Max(nextZ, chunk.transform.position.z + segmentLength);
            }
            if (mountains != null) mountainOrigin = mountains.position;
            PrepareTerrainLoop();
        }
        private void Update()
        {
            if (player == null || Time.timeScale == 0f || GameManager.Instance?.IsGameOver == true) return;
            if (mountains != null) mountains.position = mountainOrigin + Vector3.forward * player.position.z;
            AdvanceTerrainLoop();
            while (active.Count > 0 && active.Peek().transform.position.z + segmentLength < player.position.z - behindDistance)
            {
                var old = active.Dequeue();
                if (templates != null && templates.Length > 0)
                {
                    var prefab = templates[Random.Range(0, templates.Length)];
                    if (prefab != null)
                    {
                        var next = Instantiate(prefab, transform);
                        next.transform.position = new Vector3(0, 0, nextZ);
                        nextZ += segmentLength;
                        active.Enqueue(next);
                    }
                }
                Destroy(old);
            }
        }

        private void PrepareTerrainLoop()
        {
            if (terrainTiles == null || terrainTiles.Length == 0) return;

            float firstZ = float.PositiveInfinity;
            float lastZ = float.NegativeInfinity;
            foreach (var tile in terrainTiles)
            {
                if (tile == null || tile.terrainData == null) return;
                firstZ = Mathf.Min(firstZ, tile.transform.position.z);
                lastZ = Mathf.Max(lastZ, tile.transform.position.z + tile.terrainData.size.z);
            }

            terrainLength = lastZ - firstZ;
            if (terrainLength <= 0f) return;
            originalTerrainStart = firstZ;
            repeatedTerrainStart = firstZ + terrainLength;
            repeatedTerrainTiles = new Terrain[terrainTiles.Length];
            for (int i = 0; i < terrainTiles.Length; i++)
            {
                var source = terrainTiles[i];
                var copy = Instantiate(source.gameObject);
                copy.name = source.name + " (Loop)";
                copy.transform.position = source.transform.position + Vector3.forward * terrainLength;
                repeatedTerrainTiles[i] = copy.GetComponent<Terrain>();
            }
        }

        private void AdvanceTerrainLoop()
        {
            if (repeatedTerrainTiles == null) return;

            // Reutiliza os dois conjuntos de TerrainData sem criar novos terrenos a cada trecho.
            while (player.position.z > originalTerrainStart + terrainLength + behindDistance)
            {
                ShiftTerrainTiles(terrainTiles);
                originalTerrainStart += 2f * terrainLength;
            }
            while (player.position.z > repeatedTerrainStart + terrainLength + behindDistance)
            {
                ShiftTerrainTiles(repeatedTerrainTiles);
                repeatedTerrainStart += 2f * terrainLength;
            }
        }

        private void ShiftTerrainTiles(Terrain[] tiles)
        {
            foreach (var tile in tiles)
                tile.transform.position += Vector3.forward * (2f * terrainLength);
        }
    }
}
