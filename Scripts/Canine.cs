using Godot;
using System;

public partial class Canine : CharacterBody2D
{
	// the distance the canine moves every time
	[Export] public int tileSize = 32;

	[Export] public TileMapLayer obstacleLayer,
		groundLayer;

	// the direction the player moved last frame
	private Vector2 lastMovementDirection = Vector2.Zero;
	
	// distance the canine moves to get to the next tile
	private float movementDistance = 0;

	// ran during start up
	public override void _Ready()
	{
		movementDistance = tileSize;
	}

	// convert global position to the tile position at the specified tile map
	public static Vector2I PositionToAtlasIndex(Vector2 position, TileMapLayer tileMap)
	{
		return tileMap.LocalToMap(tileMap.ToLocal(position));
	}

	// get the tile type at the specified position
	public static string GetTileCustomType(Vector2I tilePos, TileMapLayer groundLayer, TileMapLayer obstacleLayer, out string obstacleTileType)
	{
		TileData tileData = groundLayer.GetCellTileData(tilePos);

		string tileCustomType = (string)tileData?.GetCustomData("CustomType");

		tileData = obstacleLayer.GetCellTileData(tilePos);

		obstacleTileType = (string)tileData?.GetCustomData("CustomType");

		return tileCustomType;
	}

	public override void _PhysicsProcess(double delta)
	{
		// read the inputs of the player
		Vector2 movementDirection = movementDirection = Input.GetVector("Left", "Right", "Up", "Down");

		// handle diagonal inputs (values get normalized so x will be ~0.7)
		if (Math.Abs(movementDirection.X) > 0 && Math.Abs(movementDirection.X) < 1)
		{
			// randomly select which input to use, if 1 is generated, select horizontal, otherwise select the vertical
			bool horizontal = GD.RandRange(0, 1) == 1;

			if (horizontal)
			{
				// keep the direction of the x component but make its magnitude 1
				movementDirection = new(movementDirection.X / Math.Abs(movementDirection.X), 0);
			}
			else
			{
				// keep the direction of the y component but make its magnitude 1
				movementDirection = new(0, movementDirection.Y / Math.Abs(movementDirection.X));
			}
		}

		// only move if last frame there was no input so you are forced to tap every time
		// i'm sammyrog
		if (lastMovementDirection == Vector2.Zero && movementDirection != Vector2.Zero)
		{
			// convert position to tile positions
			Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, obstacleLayer);
			
			// identify what the canine is currently standing on
			string currentGroundType = GetTileCustomType(currentTilePosition, groundLayer,
				obstacleLayer, out string currentObstacleType);

			// where the canine will move
			Vector2 newPosition = Position + movementDirection * movementDistance;
			
			// where the tile is that the canine will move to
			Vector2I newTilePosition = PositionToAtlasIndex(
				GetParent<Node2D>().ToGlobal(newPosition),
				obstacleLayer
			);

			// the type of tile the canine will move to
			string newGroundType = GetTileCustomType(newTilePosition, groundLayer,
				obstacleLayer, out string newObstacleType);

			// don't move if the canine will move to a rock
			if (newObstacleType == "Rock")
			{
				movementDirection = Vector2.Zero;
			}
			else
			{
				Position = newPosition;

				if (currentGroundType == "Sand")
				{
					// make sand fall after walking off that tile
					groundLayer.SetCell(currentTilePosition, 2);
				}
			}
		}

		lastMovementDirection = movementDirection;
	}

}
