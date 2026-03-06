using UnityEngine;

namespace BenScr.MinecraftClone
{
    public static class ChunkUtility
    {
        public static int GetBlockAtPosition(Vector3 worldPos) => GetBlockAtPosition(SnapPosition(worldPos));
        public static int GetBlockAtPosition(Vector3Int worldPos)
        {
            var cx = Mathf.FloorToInt((float)worldPos.x / Chunk.CHUNK_SIZE);
            var cy = Mathf.FloorToInt((float)worldPos.y / Chunk.CHUNK_HEIGHT);
            var cz = Mathf.FloorToInt((float)worldPos.z / Chunk.CHUNK_SIZE);
            var cCoord = new Vector3Int(cx, cy, cz);

            if (!TerrainGenerator.chunks.TryGetValue(cCoord, out var chunk))
                return Chunk.BLOCK_AIR;

            var lx = worldPos.x - cx * Chunk.CHUNK_SIZE;
            var ly = worldPos.y - cy * Chunk.CHUNK_HEIGHT;
            var lz = worldPos.z - cz * Chunk.CHUNK_SIZE;


            if ((uint)lx >= Chunk.CHUNK_SIZE || (uint)ly >= Chunk.CHUNK_HEIGHT || (uint)lz >= Chunk.CHUNK_SIZE)
                return Chunk.BLOCK_AIR;

            return chunk.blocks[lx, ly, lz];
        }

        public static Chunk GetChunkAtCoordinate(Vector3Int chunkCoord)
        {
            if (TerrainGenerator.chunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                return chunk;
            }

            return null;
        }
        public static Chunk GetChunkAtPosition(Vector3 position)
        {
            Vector3Int coordinate = GetChunkCoordinateFromPosition(position);

            if (TerrainGenerator.chunks.TryGetValue(new Vector3Int(coordinate.x, coordinate.y, coordinate.z), out Chunk chunk))
            {
                return chunk;
            }

            return null;
        }
        public static Chunk GetHighestChunkAt(Vector3 worldPosition)
        {
            for (int y = 0; y < 10; y++)
            {
                Vector3 pos = new Vector3(worldPosition.x, y * Chunk.CHUNK_HEIGHT + worldPosition.y, worldPosition.z);
                Chunk chunk = GetChunkAtPosition(pos);

                if (chunk.IsTop) return GetChunkAtPosition(pos);
            }
            return null;
        }

        public static bool IsInsideChunk(Vector3Int relativePosition)
        {
            if (relativePosition.x < 0 || relativePosition.y < 0 || relativePosition.z < 0 ||
                relativePosition.x > Chunk.CHUNK_SIZE - 1 || relativePosition.y > Chunk.CHUNK_HEIGHT - 1 || relativePosition.z > Chunk.CHUNK_SIZE - 1)
            {
                return false;
            }

            return true;
        }
        public static bool HasAllNeighborChunks(Vector3Int chunkCoord)
        {
            return TerrainGenerator.chunks.ContainsKey(chunkCoord + Vector3Int.right) &&
                   TerrainGenerator.chunks.ContainsKey(chunkCoord + Vector3Int.left) &&
                   TerrainGenerator.chunks.ContainsKey(chunkCoord + Vector3Int.forward) &&
                   TerrainGenerator.chunks.ContainsKey(chunkCoord + Vector3Int.back) &&
                   TerrainGenerator.chunks.ContainsKey(chunkCoord + Vector3Int.up) &&
                   TerrainGenerator.chunks.ContainsKey(chunkCoord + Vector3Int.down);
        }

        public static Vector3Int GetChunkCoordinateFromPosition(Vector3 position)
        {
            int chunkX = Mathf.FloorToInt(position.x / Chunk.CHUNK_SIZE);
            int chunkY = Mathf.FloorToInt(position.y / Chunk.CHUNK_HEIGHT);
            int chunkZ = Mathf.FloorToInt(position.z / Chunk.CHUNK_SIZE);

            return new Vector3Int(chunkX, chunkY, chunkZ);
        }

        public static Vector3Int SnapPosition(Vector3 position)
                => new Vector3Int(Mathf.FloorToInt(position.x),
                                  Mathf.FloorToInt(position.y),
                                  Mathf.FloorToInt(position.z));
    }
}
