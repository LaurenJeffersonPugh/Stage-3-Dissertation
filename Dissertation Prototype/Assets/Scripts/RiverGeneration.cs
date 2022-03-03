using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RiverGeneration : MonoBehaviour {

	[SerializeField]
	private int numberOfRivers;

	[SerializeField]
	private float heightThreshold;

	[SerializeField]
	private Color riverColor;

	public void GenerateRivers(int levelDepth, int levelWidth, LevelData levelData) {
		for (int riverIndex = 0; riverIndex < numberOfRivers; riverIndex++) {
			// choose a origin for the river
			Vector3 riverOrigin = ChooseRiverOrigin (levelDepth, levelWidth, levelData);
			// build the river starting from the origin
			BuildRiver (levelDepth, levelWidth, riverOrigin, levelData);
		}
	}

	private Vector3 ChooseRiverOrigin(int levelDepth, int levelWidth, LevelData levelData) {
		bool found = false;
		int randomZIndex = 0;
		int randomXIndex = 0;
		// iterates until finding a good river origin
		while (!found) {
			// pick a random coordinate inside the level
			randomZIndex = Random.Range (0, levelDepth);
			randomXIndex = Random.Range (0, levelWidth);

			// convert from Level Coordinate System to Tile Coordinate System and retrieve the corresponding TileData
			TileCoordinate tileCoordinate = levelData.ConvertToTileCoordinate (randomZIndex, randomXIndex);
			TileData tileData = levelData.tilesData [tileCoordinate.tileZIndex, tileCoordinate.tileXIndex];

			// if the height value of this coordinate is higher than the threshold, choose it as the river origin
			float heightValue = tileData.heightMap [tileCoordinate.coordinateZIndex, tileCoordinate.coordinateXIndex];
			if (heightValue >= this.heightThreshold) {
				found = true;
			}
		}
		return new Vector3 (randomXIndex, 0, randomZIndex);
	}

	private void BuildRiver(int levelDepth, int levelWidth, Vector3 riverOrigin, LevelData levelData) {
		HashSet<Vector3> visitedCoordinates = new HashSet<Vector3> ();
		// the first coordinate is the river origin
		Vector3 currentCoordinate = riverOrigin;
		bool foundWater = false;
		while (!foundWater) {
			// convert from Level Coordinate System to Tile Coordinate System and retrieve the corresponding TileData
			TileCoordinate tileCoordinate = levelData.ConvertToTileCoordinate ((int)currentCoordinate.z, (int)currentCoordinate.x);
			TileData tileData = levelData.tilesData [tileCoordinate.tileZIndex, tileCoordinate.tileXIndex];

			// save the current coordinate as visited
			visitedCoordinates.Add (currentCoordinate);

			// check if we have found water
			if (tileData.chosenHeightTerrainTypes [tileCoordinate.coordinateZIndex, tileCoordinate.coordinateXIndex].name == "water") {
				// if we found water, we stop
				foundWater = true;
			} else {
				// change the texture of the tileData to show a river
				tileData.texture.SetPixel (tileCoordinate.coordinateXIndex, tileCoordinate.coordinateZIndex, this.riverColor);
				tileData.texture.Apply ();

				// pick neighbor coordinates, if they exist
				List<Vector3> neighbors = new List<Vector3> ();
				if (currentCoordinate.z > 0) {
					neighbors.Add(new Vector3 (currentCoordinate.x, 0, currentCoordinate.z - 1));
				}
				if (currentCoordinate.z < levelDepth - 1) {
					neighbors.Add(new Vector3 (currentCoordinate.x, 0, currentCoordinate.z + 1));
				}
				if (currentCoordinate.x > 0) {
					neighbors.Add(new Vector3 (currentCoordinate.x - 1, 0, currentCoordinate.z));
				}
				if (currentCoordinate.x < levelWidth - 1) {
					neighbors.Add(new Vector3 (currentCoordinate.x + 1, 0, currentCoordinate.z));
				}

				// find the minimum neighbor that has not been visited yet and flow to it
				float minHeight = float.MaxValue;
				Vector3 minNeighbor = new Vector3(0, 0, 0);
				foreach (Vector3 neighbor in neighbors) {
					// convert from Level Coordinate System to Tile Coordinate System and retrieve the corresponding TileData
					TileCoordinate neighborTileCoordinate = levelData.ConvertToTileCoordinate ((int)neighbor.z, (int)neighbor.x);
					TileData neighborTileData = levelData.tilesData [neighborTileCoordinate.tileZIndex, neighborTileCoordinate.tileXIndex];

					// if the neighbor is the lowest one and has not been visited yet, save it
					float neighborHeight = tileData.heightMap [neighborTileCoordinate.coordinateZIndex, neighborTileCoordinate.coordinateXIndex];
					if (neighborHeight < minHeight && !visitedCoordinates.Contains(neighbor)) {
						minHeight = neighborHeight;
						minNeighbor = neighbor;
					}
				}
				// flow to the lowest neighbor
				currentCoordinate = minNeighbor;
			}
		}
	}
}
