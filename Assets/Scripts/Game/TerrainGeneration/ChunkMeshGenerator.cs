using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using UnityEngine;

namespace BenScr.MinecraftClone
{
    using static AssetsContainer;

    public class ChunkMeshGenerator
    {
        public static readonly ConcurrentQueue<ThreadInfo<MeshData>> meshDataThreadInfoQueue = new ConcurrentQueue<ThreadInfo<MeshData>>();

        public static void Update()
        {
            while (meshDataThreadInfoQueue.TryDequeue(out var threadInfo))
            {
                threadInfo.calback(threadInfo.parameter);
            }
        }

        public static void RequestMeshData(byte[,,] haloBlocks, Action<MeshData> callback)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                MeshDataThread(haloBlocks, callback);
            });
        }

        private static void MeshDataThread([ReadOnly] in byte[,,] haloBlocks, Action<MeshData> callback)
        {
            MeshData meshData = GenerateMeshData(haloBlocks);
            meshDataThreadInfoQueue.Enqueue(new ThreadInfo<MeshData>(callback, meshData));
        }


        public static MeshData GenerateMeshData([ReadOnly] in byte[,,] haloBlocks)
        {
            List<Vector3> solidVertices = new List<Vector3>();
            List<Vector3> solidNormals = new List<Vector3>();
            List<int> solidTriangles = new List<int>();
            List<Vector2> solidUvs = new List<Vector2>();

            List<Vector3> fluidVertices = new List<Vector3>();
            List<Vector3> fluidNormals = new List<Vector3>();
            List<int> fluidTriangles = new List<int>();
            List<Vector2> fluidUvs = new List<Vector2>();

            List<Vector3> transparentVertices = new List<Vector3>();
            List<Vector3> transparentNormals = new List<Vector3>();
            List<int> transparentTriangles = new List<int>();
            List<Vector2> transparentUvs = new List<Vector2>();

            GenerateGreedyMeshSection(
                haloBlocks,
                BlockRenderCategory.Solid,
                solidVertices,
                solidNormals,
                solidTriangles,
                solidUvs);

            GenerateGreedyMeshSection(
                haloBlocks,
                BlockRenderCategory.Fluid,
                fluidVertices,
                fluidNormals,
                fluidTriangles,
                fluidUvs);

            GenerateGreedyMeshSection(
                haloBlocks,
                BlockRenderCategory.Transparent,
                transparentVertices,
                transparentNormals,
                transparentTriangles,
                transparentUvs);

            return new MeshData(
               new MeshSection(solidTriangles, solidVertices, solidNormals, solidUvs),
               new MeshSection(fluidTriangles, fluidVertices, fluidNormals, fluidUvs),
               new MeshSection(transparentTriangles, transparentVertices, transparentNormals, transparentUvs));
        }

        private static void GenerateGreedyMeshSection(
            [ReadOnly] in byte[,,] haloBlocks,
            BlockRenderCategory category,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            List<Vector2> uvs)
        {
            int vertexIndex = 0;

            for (int face = 0; face < 6; face++)
            {
                GenerateGreedyFaceData(haloBlocks, category, face, vertices, normals, triangles, uvs, ref vertexIndex);
            }
        }

        private static void GenerateGreedyFaceData(
            [ReadOnly] in byte[,,] haloBlocks,
            BlockRenderCategory category,
            int face,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            List<Vector2> uvs,
            ref int vertexIndex)
        {
            int uSize = Chunk.CHUNK_SIZE;
            int vSize = face == 2 || face == 3 ? Chunk.CHUNK_SIZE : Chunk.CHUNK_HEIGHT;
            int depthSize = (face == 2 || face == 3) ? Chunk.CHUNK_HEIGHT : Chunk.CHUNK_SIZE;

            int[] textureMask = new int[uSize * vSize];

            for (int depth = 0; depth < depthSize; depth++)
            {
                Array.Fill(textureMask, -1);

                for (int u = 0; u < uSize; u++)
                {
                    for (int v = 0; v < vSize; v++)
                    {
                        Vector3Int pos = GetPositionForFace(face, depth, u, v);
                        int blockId = haloBlocks[pos.x + 1, pos.y + 1, pos.z + 1];

                        if (blockId == Chunk.BLOCK_AIR)
                        {
                            continue;
                        }

                        BlockData block = GetBlock(blockId);
                        if (!BelongsToCategory(block, category))
                        {
                            continue;
                        }

                        int neighborBlockId = GetHalo(haloBlocks, pos + cubeNormals[face]);
                        BlockData neighborBlock = GetBlock(neighborBlockId);

                        bool neighborIsTransparent = neighborBlock.isTransparent;
                        bool hidesFaceBecauseSameFluid = block.isFluid && neighborBlock.id == block.id;

                        if (neighborIsTransparent && !hidesFaceBecauseSameFluid)
                        {
                            textureMask[u + v * uSize] = block.GetTexture(face);
                        }
                    }
                }

                for (int v = 0; v < vSize; v++)
                {
                    for (int u = 0; u < uSize;)
                    {
                        int textureId = textureMask[u + v * uSize];
                        if (textureId < 0)
                        {
                            u++;
                            continue;
                        }

                        int width = 1;
                        while (u + width < uSize && textureMask[(u + width) + v * uSize] == textureId)
                        {
                            width++;
                        }

                        int height = 1;
                        bool done = false;
                        while (v + height < vSize && !done)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                if (textureMask[(u + k) + (v + height) * uSize] != textureId)
                                {
                                    done = true;
                                    break;
                                }
                            }

                            if (!done)
                            {
                                height++;
                            }
                        }

                        AddGreedyQuad(face, depth, u, v, width, height, textureId, vertices, normals, triangles, uvs, ref vertexIndex);

                        for (int dy = 0; dy < height; dy++)
                        {
                            for (int dx = 0; dx < width; dx++)
                            {
                                textureMask[(u + dx) + (v + dy) * uSize] = -1;
                            }
                        }

                        u += width;
                    }
                }
            }
        }

        private static bool BelongsToCategory(BlockData block, BlockRenderCategory category)
        {
            switch (category)
            {
                case BlockRenderCategory.Solid:
                    return !block.isFluid && !block.isTransparent;
                case BlockRenderCategory.Fluid:
                    return block.isFluid;
                case BlockRenderCategory.Transparent:
                    return !block.isFluid && block.isTransparent;
                default:
                    return false;
            }
        }

        private static Vector3Int GetPositionForFace(int face, int depth, int u, int v)
        {
            switch (face)
            {
                case 0:
                case 1:
                    return new Vector3Int(u, v, depth);
                case 2:
                case 3:
                    return new Vector3Int(u, depth, v);
                case 4:
                case 5:
                    return new Vector3Int(depth, v, u);
                default:
                    return Vector3Int.zero;
            }
        }

        private static void AddGreedyQuad(
           int face,
           int depth,
           int u,
           int v,
           int width,
           int height,
           int textureId,
           List<Vector3> vertices,
           List<Vector3> normals,
           List<int> triangles,
           List<Vector2> uvs,
           ref int vertexIndex)
        {
            Vector3 origin;
            Vector3 axisU;
            Vector3 axisV;

            switch (face)
            {
                case 0: // back
                    origin = new Vector3(u, v, depth);
                    axisU = new Vector3(width, 0f, 0f);
                    axisV = new Vector3(0f, height, 0f);
                    break;
                case 1: // front
                    origin = new Vector3(u + width, v, depth + 1);
                    axisU = new Vector3(-width, 0f, 0f);
                    axisV = new Vector3(0f, height, 0f);
                    break;
                case 2: // top
                    origin = new Vector3(u, depth + 1, v);
                    axisU = new Vector3(width, 0f, 0f);
                    axisV = new Vector3(0f, 0f, height);
                    break;
                case 3: // bottom
                    origin = new Vector3(u + width, depth, v);
                    axisU = new Vector3(-width, 0f, 0f);
                    axisV = new Vector3(0f, 0f, height);
                    break;
                case 4: // left
                    origin = new Vector3(depth, v, u + width);
                    axisU = new Vector3(0f, 0f, -width);
                    axisV = new Vector3(0f, height, 0f);
                    break;
                case 5: // right
                    origin = new Vector3(depth + 1, v, u);
                    axisU = new Vector3(0f, 0f, width);
                    axisV = new Vector3(0f, height, 0f);
                    break;
                default:
                    return;
            }

            vertices.Add(origin);
            vertices.Add(origin + axisV);
            vertices.Add(origin + axisU);
            vertices.Add(origin + axisU + axisV);

            for (int i = 0; i < 4; i++)
            {
                normals.Add(cubeNormals[face]);
            }

            AddTexture(textureId, ref uvs);

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 3);

            vertexIndex += 4;
        }

        private enum BlockRenderCategory
        {
            Solid,
            Fluid,
            Transparent
        }

        private static byte GetHalo(byte[,,] haloBlocks, Vector3Int pos)
        {
            return haloBlocks[pos.x + 1, pos.y + 1, pos.z + 1];
        }

        private static void AddTexture(int textureId, ref List<Vector2> uvs)
        {
            int col = textureId % TEXTURE_BLOCKS_COLS;
            int rowFromTop = TEXTURE_BLOCKS_ROWS - 1 - (textureId / TEXTURE_BLOCKS_COLS);

            float u = col * BLOCK_W;
            float v = rowFromTop * BLOCK_H;


            float epsU = 0.5f / TEXTURE_WIDTH;
            float epsV = 0.5f / TEXTURE_HEIGHT;

            float u0 = u + epsU;
            float v0 = v + epsV;
            float u1 = u + BLOCK_W - epsU;
            float v1 = v + BLOCK_H - epsV;

            uvs.Add(new Vector2(u0, v0)); // bottom-left
            uvs.Add(new Vector2(u0, v1)); // top-left
            uvs.Add(new Vector2(u1, v0)); // bottom-right
            uvs.Add(new Vector2(u1, v1)); // top-right
        }


        public static readonly Vector3[] cubeVertices = new Vector3[8] {
        new Vector3(0.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 1.0f, 1.0f),
        new Vector3(0.0f, 1.0f, 1.0f),
    };

        public static readonly Vector3Int[] cubeNormals = new Vector3Int[6] {
        new Vector3Int(0, 0, -1),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(1, 0, 0)
    };

        public static readonly int[,] cubeTriangles = new int[6, 4] {
        // Back, Front, Top, Bottom, Left, Right

		// 0 1 2 2 1 3
		{0, 3, 1, 2}, // Back Face
		{5, 6, 4, 7}, // Front Face
		{3, 7, 2, 6}, // Top Face
		{1, 5, 0, 4}, // Bottom Face
		{4, 7, 0, 3}, // Left Face
		{1, 2, 5, 6} // Right Face
	};

        public static readonly Vector2[] cubeUVs = new Vector2[4] {
        new Vector2 (0.0f, 0.0f),
        new Vector2 (0.0f, 1.0f),
        new Vector2 (1.0f, 0.0f),
        new Vector2 (1.0f, 1.0f)
    };
    }

    public struct ThreadInfo<T>
    {
        public readonly Action<T> calback;
        public readonly T parameter;

        public ThreadInfo(Action<T> callback, T parameter)
        {
            this.calback = callback;
            this.parameter = parameter;
        }
    }

    public readonly struct MeshData
    {
        public readonly MeshSection solidMesh;
        public readonly MeshSection fluidMesh;
        public readonly MeshSection transparentMesh;

        public MeshData(MeshSection solidMesh, MeshSection fluidMesh, MeshSection transparentMesh)
        {
            this.solidMesh = solidMesh;
            this.fluidMesh = fluidMesh;
            this.transparentMesh = transparentMesh;
        }
    }

    public readonly struct MeshSection
    {
        public readonly int[] triangles;
        public readonly Vector3[] vertices;
        public readonly Vector3[] normals;
        public readonly Vector2[] uvs;

        public MeshSection(List<int> triangles, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs)
        {
            this.triangles = triangles.ToArray();
            this.vertices = vertices.ToArray();
            this.normals = normals.ToArray();
            this.uvs = uvs.ToArray();
        }
    }
}