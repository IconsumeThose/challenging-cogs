using Godot;
using System.Collections.Generic;
using System;

public partial class Canine : CharacterBody2D
{
	// the distance the canine moves every time
	[Export] public int tileSize = 32;

	[Export] public float movementSpeed = 150;

	[Export] GameManager gameManager;

	[Export]
	public Control winMenu,
		pauseMenu,
		loseMenu;

	[Export] public AnimatedSprite2D animatedSprite;
	[Export] public AnimationPlayer animationPlayer;

	private List<string> blockingObstacles =
	[
		"Rock",
		"CogCrystal",
		"InforcedCogCrystal"
	] ;

	// true while the moving, false otherwise
	private bool isMoving = false,

	// true during death animation to prevent controlling the character
		isDying = false;

	// the position to move to
	private Vector2 targetPosition = Vector2.Zero;

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

	// store all useful information about a tile
	public class CustomTileData(TileData tileData)
	{
		public TileData tileData = tileData;
		public string customType = (string)tileData?.GetCustomData("CustomType");
		public Vector2 direction = GetTileDirection(tileData);
	}

	// keep track of the ground and obstacle tiles at the same position
	public class LayeredCustomTileData(CustomTileData groundTile, CustomTileData obstacleTile)
	{
		public CustomTileData groundTile = groundTile,
			obstacleTile = obstacleTile;
	}

	// get the direction the tile is facing (from alternate tiles)
	public static Vector2 GetTileDirection(TileData tileData)
	{
		if (tileData == null)
			return Vector2.Right;;

		Vector2 direction;
		if (tileData.Transpose)
		{
			if (tileData.FlipV)
			{
				direction = Vector2.Up;
			}
			else
			{
				direction = Vector2.Down;
			}
		}
		else if (tileData.FlipH)
		{
			direction = Vector2.Left;
		}
		else
		{
			direction = Vector2.Right;
		}

		return direction;
	}

	// get the tile types at the specified position
	public static LayeredCustomTileData GetTileCustomType(Vector2I tilePos, TileMapLayer groundLayer, TileMapLayer obstacleLayer)
	{
		CustomTileData groundData = new(groundLayer.GetCellTileData(tilePos));

		CustomTileData obstacleData = new(obstacleLayer.GetCellTileData(tilePos));

		return new(groundData, obstacleData);
	}

	private void Move(Vector2 newPosition)
	{
		// convert position to tile positions
		Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, gameManager.obstacleLayer);

		// identify what the canine is currently standing on
		LayeredCustomTileData currentTileData = GetTileCustomType(currentTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);

		// where the tile is that the canine will move to
		Vector2I newTilePosition = PositionToAtlasIndex(
			GetParent<Node2D>().ToGlobal(newPosition),
			gameManager.obstacleLayer
		);

		// the type of tile the canine will move to
		LayeredCustomTileData newTileData = GetTileCustomType(newTilePosition, gameManager.groundLayer,
			gameManager.obstacleLayer);

		Vector2 movementDirection = (newPosition - Position).Normalized();

		// flip sprite accordingly when moving horizontally
		if (movementDirection.X > 0)
		{
			animatedSprite.FlipH = false;
		}
		else if (movementDirection.X < 0)
		{
			animatedSprite.FlipH = true;
		}

		// don't move if the canine will move to a rock
		if (!blockingObstacles.Contains(newTileData.obstacleTile.customType))
		{
			isMoving = true;
			targetPosition = newPosition;

			// set animation accordingly to the current tile
			if (currentTileData.groundTile.customType == "Conveyor")
			{
				animatedSprite.Animation = "Idle";
			}
			else
			{
				animatedSprite.Animation = "Move";
			}


			if (currentTileData.groundTile.customType == "Sand")
			{
				// make sand fall after walking off that tile
				gameManager.groundLayer.SetCell(currentTilePosition, 1, new(2, 0));
			}
		}
	}

	// toggle FlipH to make the sprite look the opposite way, called from animation player
	public void ToggleSpriteFlipH()
	{
		animatedSprite.FlipH = !animatedSprite.FlipH;
	}

	// open the lose menu, called from animation player
	public void Lose()
	{
		Engine.TimeScale = 0;
		loseMenu.Visible = true;
	}

	// runs every physics frame
	public override void _PhysicsProcess(double delta)
	{
		// toggle pause menu only if no other menu is visible
		if (Input.IsActionJustPressed("Pause") && !winMenu.Visible && !loseMenu.Visible)
		{
			pauseMenu.Visible = !pauseMenu.Visible;

			Engine.TimeScale = Math.Abs(Engine.TimeScale - 1);
		}

		// don't allow controlling character while game is paused
		if (Engine.TimeScale == 0)
			return;

		// reset the level when reset button is pressed
		if (Input.IsActionJustPressed("Reset"))
		{
			GetTree().ChangeSceneToFile("res://Scenes/level.tscn");
		}

		// don't allow controlling character while dying but allow resetting
		if (isDying)
			return;

		// move the canine to target position
		if (isMoving)
		{
			Vector2 movementDirection = (targetPosition - Position).Normalized();

			Velocity = movementDirection * movementSpeed;

			MoveAndSlide();

			// stop moving when close enough to target position
			if ((Position - targetPosition).Length() < 1)
			{
				// set position exactly to target
				Position = targetPosition;
				isMoving = false;

				// set animation to idle
				animatedSprite.Animation = "Idle";
				
				// where the tile is that the canine will move to
				Vector2I newTilePosition = PositionToAtlasIndex(
					GetParent<Node2D>().ToGlobal(Position),
					gameManager.obstacleLayer
				);

				// the type of tile the canine will move to
				LayeredCustomTileData newTileData = GetTileCustomType(newTilePosition, gameManager.groundLayer,
					gameManager.obstacleLayer);

				if (newTileData.groundTile.customType == "GoalOn")
				{
					Engine.TimeScale = 0;
					winMenu.Visible = true;
				}
				else if (newTileData.groundTile.customType == "Void")
				{
					isDying = true;
					animationPlayer.Play("Fall");
				}
				else if (newTileData.groundTile.customType == "Ice")
				{
					Vector2 newPosition = Position + movementDirection * movementDistance;
					Move(newPosition);
				}
				else if (newTileData.groundTile.customType == "Conveyor")
				{
					Vector2 newPosition = Position + newTileData.groundTile.direction * movementDistance;
					Move(newPosition);
				}
				else if (newTileData.obstacleTile.customType == "Cog")
				{
					gameManager.CogChallenged();
					gameManager.obstacleLayer.SetCell(newTilePosition);
				}
			}
			return;
		}

		// read the inputs of the player
		Vector2 inputDirection = Input.GetVector("Left", "Right", "Up", "Down");

		// handle diagonal inputs (values get normalized so x will be ~0.7)
		if (Math.Abs(inputDirection.X) > 0 && Math.Abs(inputDirection.X) < 1)
		{
			// randomly select which input to use, if 1 is generated, select horizontal, otherwise select the vertical
			bool horizontal = GD.RandRange(0, 1) == 1;

			if (horizontal)
			{
				// keep the direction of the x component but make its magnitude 1
				inputDirection = new(inputDirection.X / Math.Abs(inputDirection.X), 0);
			}
			else
			{
				// keep the direction of the y component but make its magnitude 1
				inputDirection = new(0, inputDirection.Y / Math.Abs(inputDirection.X));
			}
		}

		// i'm sammyrog
		if (inputDirection != Vector2.Zero)
		{
			// where the canine will move
			Vector2 newPosition = Position + inputDirection * movementDistance;

			Move(newPosition);
		}
		// don't allow paradigm shifting if none are remainging
		else if (Input.IsActionJustPressed("ParadigmShift") && gameManager.paradigmShiftsRemaining > 0)
		{
			// game manager updates the remaining count
			gameManager.ParadigmShifted();

			// convert position to tile positions
			Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, gameManager.obstacleLayer);

			// get all adjacent tile coordinates
			List<Vector2I> adjacentCoordinates =
			[
				currentTilePosition + Vector2I.Left,
				currentTilePosition + Vector2I.Right,
				currentTilePosition + Vector2I.Down,
				currentTilePosition + Vector2I.Up	
			];

			// get all diagonal tile coordinates
			List<Vector2I> diagonalCoordinates =
			[
				currentTilePosition + Vector2I.Left + Vector2I.Down,
				currentTilePosition + Vector2I.Right + Vector2I.Down,
				currentTilePosition + Vector2I.Left + Vector2I.Up,
				currentTilePosition + Vector2I.Right + Vector2I.Up		
			];

			// replace both CogCrystals and InforcedCogCrystals with a cog for adjacent tiles
			foreach (Vector2I adjacentCoordinate in adjacentCoordinates)
			{
				CustomTileData obstacleData = new(gameManager.obstacleLayer.GetCellTileData(adjacentCoordinate));

				if (obstacleData.customType == "CogCrystal" || obstacleData.customType == "InforcedCogCrystal")
				{
					gameManager.obstacleLayer.SetCell(adjacentCoordinate, 1, new(5, 1));
				}
			}
			
			// replace only normal CogCrystals with a cog for diagonal tiles
			foreach (Vector2I diagonalCoordinate in diagonalCoordinates)
			{
				CustomTileData obstacleData = new(gameManager.obstacleLayer.GetCellTileData(diagonalCoordinate));

				if (obstacleData.customType == "CogCrystal")
				{
					gameManager.obstacleLayer.SetCell(diagonalCoordinate, 1, new(5, 1));
				}
			}
		}
	}
}
