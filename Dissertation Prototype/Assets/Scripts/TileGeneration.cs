using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileGeneration : MonoBehaviour {

	public Shader shader;

	[SerializeField]
	NoiseMapGeneration noiseMapGeneration;

	[SerializeField]
	private MeshRenderer tileRenderer;

	[SerializeField]
	private MeshFilter meshFilter;

	[SerializeField]
	private MeshCollider meshCollider;

	[SerializeField]
	private float levelScale;

	[SerializeField]
	private TerrainType[] heightTerrainTypes;

	[SerializeField]
	private TerrainType[] heatTerrainTypes;

	[SerializeField]
	private TerrainType[] moistureTerrainTypes;

	[SerializeField]
	private float heightMultiplier;

	[SerializeField]
	private AnimationCurve heightCurve;

	[SerializeField]
	private AnimationCurve heatCurve;

	[SerializeField]
	private AnimationCurve moistureCurve;

	[SerializeField]
	private Wave[] heightWaves;

	[SerializeField]
	private Wave[] heatWaves;

	[SerializeField]
	private Wave[] moistureWaves;

	[SerializeField]
	private BiomeRow[] biomes;

	[SerializeField]
	private Color waterColor;

	[SerializeField]
	private Texture2D waterTexture;

	[SerializeField]
	private VisualizationMode visualizationMode;

	public TileData GenerateTile(float centerVertexZ, float maxDistanceZ) {
		// calculate tile depth and width based on the mesh vertices
		Vector3[] meshVertices = this.meshFilter.mesh.vertices;
		int tileDepth = (int)Mathf.Sqrt (meshVertices.Length);
		int tileWidth = tileDepth;

		// calculate the offsets based on the tile position
		float offsetX = -this.gameObject.transform.position.x;
		float offsetZ = -this.gameObject.transform.position.z;

		// generate a heightMap using Perlin Noise
		float[,] heightMap = this.noiseMapGeneration.GeneratePerlinNoiseMap (tileDepth, tileWidth, this.levelScale, offsetX, offsetZ, this.heightWaves);

		// calculate vertex offset based on the Tile position and the distance between vertices
		Vector3 tileDimensions = this.meshFilter.mesh.bounds.size;
		float distanceBetweenVertices = tileDimensions.z / (float)tileDepth;
		float vertexOffsetZ = this.gameObject.transform.position.z / distanceBetweenVertices;

		// generate a heatMap using uniform noise
		float[,] uniformHeatMap = this.noiseMapGeneration.GenerateUniformNoiseMap (tileDepth, tileWidth, centerVertexZ, maxDistanceZ, vertexOffsetZ);
		// generate a heatMap using Perlin Noise
		float[,] randomHeatMap = this.noiseMapGeneration.GeneratePerlinNoiseMap (tileDepth, tileWidth, this.levelScale, offsetX, offsetZ, this.heatWaves);
		float[,] heatMap = new float[tileDepth, tileWidth];
		for (int zIndex = 0; zIndex < tileDepth; zIndex++) {
			for (int xIndex = 0; xIndex < tileWidth; xIndex++) {
				// mix both heat maps together by multiplying their values
				heatMap [zIndex, xIndex] = uniformHeatMap [zIndex, xIndex] * randomHeatMap [zIndex, xIndex];
				// makes higher regions colder, by adding the height value to the heat map
				heatMap [zIndex, xIndex] += this.heatCurve.Evaluate(heightMap [zIndex, xIndex]) * heightMap [zIndex, xIndex];
			}
		}

		// generate a moistureMap using Perlin Noise
		float[,] moistureMap = this.noiseMapGeneration.GeneratePerlinNoiseMap (tileDepth, tileWidth, this.levelScale, offsetX, offsetZ, this.moistureWaves);
		for (int zIndex = 0; zIndex < tileDepth; zIndex++) {
			for (int xIndex = 0; xIndex < tileWidth; xIndex++) {
				// makes higher regions dryer, by reducing the height value from the heat map
				moistureMap [zIndex, xIndex] -= this.moistureCurve.Evaluate(heightMap [zIndex, xIndex]) * heightMap [zIndex, xIndex];
			}
		}

		// build a Texture2D from the height map
		TerrainType[,] chosenHeightTerrainTypes = new TerrainType[tileDepth, tileWidth];
		Texture2D heightTexture = BuildTexture (heightMap, this.heightTerrainTypes, chosenHeightTerrainTypes);
		// build a Texture2D from the heat map
		TerrainType[,] chosenHeatTerrainTypes = new TerrainType[tileDepth, tileWidth];
		Texture2D heatTexture = BuildTexture (heatMap, this.heatTerrainTypes, chosenHeatTerrainTypes);
		// build a Texture2D from the moisture map
		TerrainType[,] chosenMoistureTerrainTypes = new TerrainType[tileDepth, tileWidth];
		Texture2D moistureTexture = BuildTexture (moistureMap, this.moistureTerrainTypes, chosenMoistureTerrainTypes);

		// build a biomes Texture2D from the three other noise variables
		Biome[,] chosenBiomes = new Biome[tileDepth, tileWidth];
		BuildBiomeTexture(chosenHeightTerrainTypes, chosenHeatTerrainTypes, chosenMoistureTerrainTypes, chosenBiomes);

		switch (this.visualizationMode) {
		case VisualizationMode.Height:
			// assign material texture to be the heightTexture
			this.tileRenderer.material.mainTexture = heightTexture;
			break;
		case VisualizationMode.Heat:
			// assign material texture to be the heatTexture
			this.tileRenderer.material.mainTexture = heatTexture;
			break;
		case VisualizationMode.Moisture:
			// assign material texture to be the moistureTexture
			this.tileRenderer.material.mainTexture = moistureTexture;
			break;
		case VisualizationMode.Biome:
			// assign material texture to be the moistureTexture
			//this.tileRenderer.material.mainTexture = biomeTexture;
			break;
		}

		// update the tile mesh vertices according to the height map
		UpdateMeshVertices (heightMap);

		TileData tileData = new TileData (heightMap, heatMap, moistureMap, 
			chosenHeightTerrainTypes, chosenHeatTerrainTypes, chosenMoistureTerrainTypes, chosenBiomes, 
			this.meshFilter.mesh, (Texture2D)this.tileRenderer.material.mainTexture);

		
		return tileData;
	}

	private Texture2D BuildTexture(float[,] heightMap, TerrainType[] terrainTypes, TerrainType[,] chosenTerrainTypes) {
		int tileDepth = heightMap.GetLength (0);
		int tileWidth = heightMap.GetLength (1);

		Color[] colorMap = new Color[tileDepth * tileWidth];
		for (int zIndex = 0; zIndex < tileDepth; zIndex++) {
			for (int xIndex = 0; xIndex < tileWidth; xIndex++) {
				// transform the 2D map index is an Array index
				int colorIndex = zIndex * tileWidth + xIndex;
				float height = heightMap [zIndex, xIndex];
				// choose a terrain type according to the height value
				TerrainType terrainType = ChooseTerrainType (height, terrainTypes);
				// assign the color according to the terrain type
				colorMap[colorIndex] = terrainType.color;

				// save the chosen terrain type
				chosenTerrainTypes [zIndex, xIndex] = terrainType;
			}
		}

		// create a new texture and set its pixel colors
		Texture2D tileTexture = new Texture2D (tileWidth, tileDepth);
		tileTexture.wrapMode = TextureWrapMode.Clamp;
		tileTexture.SetPixels (colorMap);
		tileTexture.Apply ();

		return tileTexture;
	}

	/*private Texture2D[] BuildBiomeTexture(float[,] heightMap, TerrainType[] terrainTypes, TerrainType[,] chosenTerrainTypes)
	{
		int tileDepth = heightMap.GetLength(0);
		int tileWidth = heightMap.GetLength(1);
		int size = 5;

		float[,,] alphaMaps = new float[size, tileDepth, tileWidth];
		for (int zIndex = 0; zIndex < tileDepth; zIndex++)
		{
			for (int xIndex = 0; xIndex < tileWidth; xIndex++)
			{
				float height = heightMap[zIndex, xIndex];
				TerrainType terrainType = ChooseTerrainType(height, terrainTypes);
				alphaMaps[terrainType.index, xIndex, zIndex] = 1;

			}
		}

		Normalise(alphaMaps, heightMap, terrainTypes, chosenTerrainTypes);
		Texture2D[] textureList= new Texture2D[alphaMaps.Length]; 

		for (int textureIndex=0; textureIndex < alphaMaps.Length; textureIndex++) {
			Color[] colorMap = new Color[tileDepth * tileWidth];
			for (int zIndex = 0; zIndex < tileDepth; zIndex++)
			{
				for (int xIndex = 0; xIndex < tileWidth; xIndex++)
				{
				
					colorMap[textureIndex] = new Color(0,0,0,alphaMaps[textureIndex,xIndex, zIndex]);

				}
			}
			Texture2D tileTexture = new Texture2D(tileWidth, tileDepth);
			tileTexture.wrapMode = TextureWrapMode.Clamp;
			tileTexture.SetPixels(colorMap);
			tileTexture.Apply();
			textureList[textureIndex] = tileTexture;
		}

		
		mat.SetTexture("_alphamap1",textureList[0]);
		mat.SetTexture("_alphamap2", textureList[1]);
		mat.SetTexture("_alphamap3", textureList[2]);
		mat.SetTexture("_alphamap4", textureList[3]);
		mat.SetTexture("_alphamap5", textureList[4]);
		return textureList;

	}*/


	private void Normalise(float[,,] alphaMaps, float[,] heightMap, TerrainType[] terrainTypes, TerrainType[,] chosenTerrainTypes)
    {
		int tileDepth = heightMap.GetLength(0);
		int tileWidth = heightMap.GetLength(1);
		for (int z = 0; z < tileDepth; z++)
		{
			for (int x = 0; x < tileWidth; x++)
			{

				float total = 0;
				for (int textureIndex = 0; textureIndex < alphaMaps.Length; textureIndex++)
				{
					total += alphaMaps[textureIndex,z,x];
				}
				for (int textureIndex = 0; textureIndex < alphaMaps.Length; textureIndex++)
				{
					alphaMaps[textureIndex, z, x] /= total;
				}
			}
		}
	}


	TerrainType ChooseTerrainType(float noise, TerrainType[] terrainTypes) {
		// for each terrain type, check if the height is lower than the one for the terrain type
		foreach (TerrainType terrainType in terrainTypes) {
			// return the first terrain type whose height is higher than the generated one
			if (noise < terrainType.threshold) {
				return terrainType;
			}
		}
		return terrainTypes [terrainTypes.Length - 1];
	}

	private void UpdateMeshVertices(float[,] heightMap) {
		int tileDepth = heightMap.GetLength (0);
		int tileWidth = heightMap.GetLength (1);

		Vector3[] meshVertices = this.meshFilter.mesh.vertices;

		// iterate through all the heightMap coordinates, updating the vertex index
		int vertexIndex = 0;
		for (int zIndex = 0; zIndex < tileDepth; zIndex++) {
			for (int xIndex = 0; xIndex < tileWidth; xIndex++) {
				float height = heightMap [zIndex, xIndex];

				Vector3 vertex = meshVertices [vertexIndex];
				// change the vertex Y coordinate, proportional to the height value. The height value is evaluated by the heightCurve function, in order to correct it.
				meshVertices[vertexIndex] = new Vector3(vertex.x, this.heightCurve.Evaluate(height) * this.heightMultiplier, vertex.z);

				vertexIndex++;
			}
		}

		// update the vertices in the mesh and update its properties
		this.meshFilter.mesh.vertices = meshVertices;
		this.meshFilter.mesh.RecalculateBounds ();
		this.meshFilter.mesh.RecalculateNormals ();
		// update the mesh collider
		this.meshCollider.sharedMesh = this.meshFilter.mesh;
	}

	private void BuildBiomeTexture(TerrainType[,] heightTerrainTypes, TerrainType[,] heatTerrainTypes, TerrainType[,] moistureTerrainTypes, Biome[,] chosenBiomes) {
		int tileDepth = heatTerrainTypes.GetLength (0);
		int tileWidth = heatTerrainTypes.GetLength (1);
		Material mat = new Material(shader);

		int numberOfTextures = 7;

		Color[][] alphaColorMaps = new Color[numberOfTextures][];

		for(int i=0; i<numberOfTextures; i++)
        {
			alphaColorMaps[i] = new Color[tileDepth * tileWidth];
        }
		
		for (int zIndex = 0; zIndex < tileDepth; zIndex++) {
			for (int xIndex = 0; xIndex < tileWidth; xIndex++) {
				int colorIndex = zIndex * tileWidth + xIndex;

				TerrainType heightTerrainType = heightTerrainTypes [zIndex, xIndex];
				// check if the current coordinate is a water region
				if (heightTerrainType.name != "water") {
					// if a coordinate is not water, its biome will be defined by the heat and moisture values
					TerrainType heatTerrainType = heatTerrainTypes [zIndex, xIndex];
					TerrainType moistureTerrainType = moistureTerrainTypes [zIndex, xIndex];

					// terrain type index is used to access the biomes table
					Biome biome = this.biomes [moistureTerrainType.index].biomes [heatTerrainType.index];
					// assign the color according to the selected biome
					//colorMap[colorIndex] = biome.color;

					if (biome.name == "desert")
                    {
						//add to alpha map
						alphaColorMaps[0][colorIndex] = new Color(1, 1, 0, 1);
						//pass texture to shader
						mat.SetTexture("_texture1", biome.texture);

					}
					if (biome.name=="boreal forest")
                    {
						alphaColorMaps[1][colorIndex] = new Color(0, 1, 0, 1);
						mat.SetTexture("_texture2", biome.texture);
					}
					if (biome.name == "tundra")
                    {
						alphaColorMaps[2][colorIndex] = new Color(1, 1, 1, 1);
						mat.SetTexture("_texture3", biome.texture);
					}
					if (biome.name == "savanna")
					{
						alphaColorMaps[3][colorIndex] = new Color(1, 0, 0, 1);
						mat.SetTexture("_texture4", biome.texture);
					}
					if (biome.name == "rainforest")
					{
						alphaColorMaps[5][colorIndex] = new Color(1, 0, 0, 1);
						mat.SetTexture("_texture6", biome.texture);
					}
					if (biome.name == "shrubland")
					{
						alphaColorMaps[6][colorIndex] = new Color(1, 0, 0, 1);
						mat.SetTexture("_texture7", biome.texture);
					}

					// save biome in chosenBiomes matrix only when it is not water
					chosenBiomes [zIndex, xIndex] = biome;

				} else {
					// water regions don't have biomes, they always have the same color		
					alphaColorMaps[4][colorIndex] = new Color(0, 0, 1, 1);
					mat.SetTexture("_texture5", this.waterTexture);
				}
			}
		}
		Texture2D[] alphaTextures= new Texture2D[numberOfTextures];
		for (int i = 0; i < alphaColorMaps.Length; i++)

		{
			alphaTextures[i] = new Texture2D(tileWidth, tileDepth);
			alphaTextures[i].wrapMode = TextureWrapMode.Clamp;
			alphaTextures[i].SetPixels(alphaColorMaps[i]);
			alphaTextures[i].Apply();
		}
		
		mat.SetTexture("_alphamap1", alphaTextures[0]);
		mat.SetTexture("_alphamap2", alphaTextures[1]);
		mat.SetTexture("_alphamap3", alphaTextures[2]);
		mat.SetTexture("_alphamap4", alphaTextures[3]);
		mat.SetTexture("_alphamap5", alphaTextures[4]);
		mat.SetTexture("_alphamap6", alphaTextures[5]);
		mat.SetTexture("_alphamap7", alphaTextures[6]);
		

		gameObject.GetComponent<MeshRenderer>().materials = new Material[] { mat };
	}
}

[System.Serializable]
public class TerrainType {
	public string name;
	public float threshold;
	public Color color;
	public int index;
}

[System.Serializable]
public class Biome {
	public string name;
	public Color color;
	public int index;
	public Texture2D texture;
}

[System.Serializable]
public class BiomeRow {
	public Biome[] biomes;
}

// class to store all data for a single tile
public class TileData {
	public float[,]  heightMap;
	public float[,]  heatMap;
	public float[,]  moistureMap;
	public TerrainType[,] chosenHeightTerrainTypes;
	public TerrainType[,] chosenHeatTerrainTypes;
	public TerrainType[,] chosenMoistureTerrainTypes;
	public Biome[,] chosenBiomes;
	public Mesh mesh;
	public Texture2D texture;

	public TileData(float[,]  heightMap, float[,]  heatMap, float[,]  moistureMap, 
		TerrainType[,] chosenHeightTerrainTypes, TerrainType[,] chosenHeatTerrainTypes, TerrainType[,] chosenMoistureTerrainTypes,
		Biome[,] chosenBiomes, Mesh mesh, Texture2D texture) {
		this.heightMap = heightMap;
		this.heatMap = heatMap;
		this.moistureMap = moistureMap;
		this.chosenHeightTerrainTypes = chosenHeightTerrainTypes;
		this.chosenHeatTerrainTypes = chosenHeatTerrainTypes;
		this.chosenMoistureTerrainTypes = chosenMoistureTerrainTypes;
		this.chosenBiomes = chosenBiomes;
		this.mesh = mesh;
		this.texture = texture;
	}
}

enum VisualizationMode {Height, Heat, Moisture, Biome}
