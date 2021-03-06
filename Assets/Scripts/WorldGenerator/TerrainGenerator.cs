using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TerrainGenerator : MonoBehaviour
{
	[Header("General Setting")]
	[SerializeField]
	private GameObject blockRef;
	[SerializeField]
	private Material material;
	[Space()]
	[SerializeField]
	private int Width = 50;
	[SerializeField]
	private int Height = 10;
	[Space()]
	[SerializeField]
	private Vector3Int ChunkSize;


	[Header("Noise Setting")]
	[SerializeField, Range(0f, 1f)]
	private float terrainSurface = 0.5f;
	[SerializeField]
	private float noiseScale = 4f;
	[SerializeField]
	private Vector2 offset;

	private bool[,,] blocks;
	private float[,,] terrainMap;

	private Vector3Int GridSize;
	private Vector3 bounds;
	private Bounds terrainBounds;
	private List<Chunk> chunks = new List<Chunk>();

    //-----------Object Controll-----------//

    private void Awake()
    {
		terrainMap = new float[Width * ChunkSize.x + 1, Height + 1, Width * ChunkSize.z + 1];
		GridSize = new Vector3Int(Width * ChunkSize.x, Height, Width * ChunkSize.z);
		blocks = new bool[Width * ChunkSize.x, Height, Width * ChunkSize.z];

		for (int x = 0; x < Width * ChunkSize.x + 1; x++)
			for (int z = 0; z < Width * ChunkSize.z + 1; z++)
				for (int y = 0; y < Height; y++) terrainMap[x, y, z] = 1f;
	}

    private void OnEnable()
    {
		bounds = blockRef.GetComponent<MeshFilter>().sharedMesh.bounds.size;
		terrainBounds = CalculateTerrainBounds();
	}

    public void GenerateTerrain()
    {
		PopulateTerrainMap();

		InitChunks();
		UpdateChunksMeshData();
		CreateAllVisibleBlocks();
	}

	//-----------Terrain Controll-----------//

	private void PopulateTerrainMap()
	{
		Vector3 position = Vector3.zero;
		float thisHeight;
		float point;

		for (position.x = 0; position.x < GridSize.x; position.x++)
		{
			for (position.z = 0; position.z < GridSize.z; position.z++)
			{
				for (position.y = 0; position.y < GridSize.y; position.y++)
				{
					thisHeight = (float)GridSize.y * Mathf.PerlinNoise((float)position.x * noiseScale + offset.x, (float)position.z * noiseScale + offset.y);

					if (position.y <= thisHeight - (1 - terrainSurface))
						point = 0f;
					else point = 1f;

					blocks[(int)position.x, (int)position.y, (int)position.z] = point == 0f;

					if (blocks[(int)position.x, (int)position.y, (int)position.z])
					{
						FillBlock(ref position, 0f);
					}
					else terrainMap[(int)position.x, (int)position.y, (int)position.z] = 1f;
				}
			}
		}
	}

	private void UpdateTerrainMapAroundBlock(ref Vector3 position)
	{
		Vector3 neighbourPosition;
		Vector3Int neighbours = Vector3Int.zero;
		for (neighbours.x = -1; neighbours.x <= 1; neighbours.x++)
		{
			for (neighbours.y = -1; neighbours.y <= 1; neighbours.y++)
			{
				for (neighbours.z = -1; neighbours.z <= 1; neighbours.z++)
				{
					neighbourPosition = position + neighbours;
					if (IsPositionInBounds(neighbourPosition))
					{
						Vector3Int pos = new Vector3Int((int)neighbourPosition.x, (int)neighbourPosition.y, (int)neighbourPosition.z);
						if (blocks[pos.x, pos.y, pos.z] == true)
							terrainMap[(int)neighbourPosition.x, (int)neighbourPosition.y, (int)neighbourPosition.z] = 0f;
						else terrainMap[(int)neighbourPosition.x, (int)neighbourPosition.y, (int)neighbourPosition.z] = 1f;
					}
				}
			}
		}
	}

	private void FillBlock(ref Vector3 position, float value)
	{
		Vector3 corner;
		for (int i = 0; i < 8; i++)
		{
			corner = position + Cube.CornerTable[i];
			terrainMap[(int)corner.x, (int)corner.y, (int)corner.z] = value;
		}
	}

	//-----------Chunks Controll-----------//

	private void InitChunks()
	{
		for (int x = 0; x < Width; x++)
			for (int z = 0; z < Width; z++)
				chunks.Add(Chunk.ChunkFabric.Create(new Vector3Int(x, 0, z), material, transform, bounds, DestroyBlock, AddBlock));
	}

	private void CreateAllVisibleBlocks()
	{
		Vector3 position = Vector3.zero;
		for (position.x = 0; position.x < GridSize.x; position.x++)
		{
			for (position.z = 0; position.z < GridSize.z; position.z++)
			{
				for (position.y = 0; position.y < GridSize.y; position.y++)
				{
					if (IsBlockVisible(ref position))
					{
						GetBlocksChunk(ref position).AddBlock(position);
					}
				}
			}
		}
	}

    private void UpdateChunksMeshData()
    {
		foreach (Chunk chunk in chunks)
		{
			UpdateChunkMeshData(chunk);
		}
	}

    private void UpdateChunkMeshData(Chunk chunk)
	{
		float[] cube = new float[Cube.CornersAmount];
		MeshBuilder meshBuilder = new MeshBuilder();

		Vector3Int position = new Vector3Int(chunk.GetCoords().x * ChunkSize.x, GridSize.y - 1, chunk.GetCoords().z * ChunkSize.z);
		Vector3Int limit = position + ChunkSize;
		Vector3Int corner;

		for (position.x = chunk.GetCoords().x * ChunkSize.x; position.x < limit.x; position.x++)
		{
			for (position.z = chunk.GetCoords().z * ChunkSize.z; position.z < limit.z; position.z++)
			{
				for (position.y = 0; position.y < limit.y; position.y++)
				{
					for (int i = 0; i < cube.Length; i++)
					{
						corner = position + Cube.CornerTable[i];
						cube[i] = terrainMap[corner.x, corner.y, corner.z];
					}

					meshBuilder.AddVertices(MarchingCubes.MarchCube(position, bounds, cube));
				}
			}
		}
		chunk.SetMesh(meshBuilder.BuildMesh());
	}

	//-----------Blocks Controll-----------//

	public void DestroyBlock(Vector3 position)
	{
		if (blocks[(int)position.x, (int)position.y, (int)position.z] != false)
		{
			blocks[(int)position.x, (int)position.y, (int)position.z] = false;

			UpdateNeighboursVisibility(ref position);
			UpdateTerrainMapAroundBlock(ref position);
			UpdateChunkMeshData(GetBlocksChunk(ref position));
			for (int i = -1; i <= 1; i++)
				for (int j = -1; j <= 1; j++)
				{ 
					Vector3 pos = new Vector3(position.x + i, position.y, position.z + i);
					if (GetBlocksChunk(ref position) != GetBlocksChunk(ref pos))
						UpdateChunkMeshData(GetBlocksChunk(ref pos));
				}
		}
	}

	public void AddBlock(Vector3 position)
	{
		if (blocks[(int)position.x, (int)position.y, (int)position.z] != true)
		{
			blocks[(int)position.x, (int)position.y, (int)position.z] = true;

			UpdateNeighboursVisibility(ref position);
			UpdateTerrainMapAroundBlock(ref position);
			UpdateChunkMeshData(GetBlocksChunk(ref position));
		}
	}

	private void UpdateNeighboursVisibility(ref Vector3 position)
	{
		Vector3 neighbourPosition;

		for (Faces face = 0; face < Faces.Zero; face++)
		{
			neighbourPosition = Cube.GetFaceNormal(face) + position;

			if (IsPositionInBounds(neighbourPosition))
			{
				if (IsBlockVisible(ref neighbourPosition)) GetBlocksChunk(ref neighbourPosition).EnableBlock(neighbourPosition);
				else GetBlocksChunk(ref neighbourPosition).DisableBlock(neighbourPosition);
			}
			else continue;
		}
	}

	private bool IsBlockVisible(ref Vector3 position)
	{
		if (!blocks[(int)position.x, (int)position.y, (int)position.z]) return false;

		Vector3 neighbourPosition;

		for (Faces face = 0; face < Faces.Zero; face++)
		{
			neighbourPosition = Cube.GetFaceNormal(face) + position;

			if (IsPositionInBounds(neighbourPosition))
			{
				if (!blocks[(int)neighbourPosition.x, (int)neighbourPosition.y, (int)neighbourPosition.z]) return true;
			}
			else continue;
		}
		return false;
	}

    private Chunk GetBlocksChunk(ref Vector3 position)
    {
        Vector3Int coord;
        foreach (Chunk chunk in chunks)
        {
            coord = new Vector3Int((int)position.x / ChunkSize.x, 0, (int)position.z / ChunkSize.z);
            if (chunk.GetCoords() == coord)
                return chunk;
        }
        return null;
    }

	//-----------getters/setters-----------//

	public Vector3 GetTerrainCenter()
    {
		return terrainBounds.center;
    }

	public Bounds GetTerrainBounds()
	{
		return terrainBounds;
	}

	//-----------Others-----------//

	private bool IsPositionInBounds(Vector3 position)
    {
		return (position.x >= 0 && position.y >= 0 && position.z >= 0
				&& position.x <GridSize.x && position.y < GridSize.y && position.z < GridSize.z);
    }

	private Vector3 CalculateTerrainCenter()
    {
		return new Vector3(GridSize.x * bounds.x / 2, GridSize.y * bounds.y / 2, GridSize.z * bounds.z / 2) + transform.position;
	}

	private Bounds CalculateTerrainBounds()
	{
		return new Bounds(CalculateTerrainCenter(), new Vector3(GridSize.x * bounds.x, GridSize.y * bounds.y, GridSize.z * bounds.z));
	}
}