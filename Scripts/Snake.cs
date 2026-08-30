using Godot;
using static GameManager;
#pragma warning disable CA1050
public partial class Snake : Character
{
	/** <summary>Used for snakeDirection</summary> */
	public enum StartingSnakeDirection
	{
		upOrRight,
		downOrLeft
	}

	/** <summary>Used for startingSnakeDirection</summary> */
	public enum SnakeDirection
	{
		horizontal,
		vertical
	}

	/** <summary>The axis the snake will move along: either vertical or horizontal</summary> */
	[Export] public SnakeDirection snakeDirection = SnakeDirection.vertical;

	/** <summary>The direction the snake starts in which also depends on if the snake will move vertically or horizontally</summary> */
	[Export] public StartingSnakeDirection startingSnakeDirection = StartingSnakeDirection.upOrRight;
	
	[Export] public Texture2D enemySpriteSheet;

	/** <summary>The current direction the snake will move in along its dedicated axis</summary> */
	public Vector2 direction = Vector2.Up;

	/** <summary>Flag that is set to true when the snake's turn was triggered by Cogito movement</summary> */
	public bool queueMove = false,

		/** <summary>Flag that tracks if the other direction was tried for this movement turn</summary> */
		triedOtherDirection = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (snakeDirection == SnakeDirection.horizontal)
		{
			direction = Vector2.Right;
		}
		else
		{
			animatedSprite.SpriteFrames = (SpriteFrames)animatedSprite.SpriteFrames.Duplicate();
			SpriteFrames snakeFrames = animatedSprite.SpriteFrames;

			snakeFrames.Clear("Idle");
			snakeFrames.Clear("Move");

			// instantiate new atlas textures to create snertical sprites
			AtlasTexture snerticalIdle0 = new(),
				snerticalIdle1 = new(),
				snerticalMove0 = new(),
				snerticalMove1 = new();

			// set atlas texture for all animation frames to sprite sheet
			snerticalIdle0.Atlas = snerticalIdle1.Atlas = snerticalMove0.Atlas = snerticalMove1.Atlas = enemySpriteSheet;

			// set texture regions to where the associated tiles are
			snerticalIdle0.Region = new(tileSize * new Vector2(0, 1), tileSize * new Vector2(1, 1));
			snerticalIdle1.Region = new(tileSize * new Vector2(1, 1), tileSize * new Vector2(1, 1));
			snerticalMove0.Region = new(tileSize * new Vector2(2, 1), tileSize * new Vector2(1, 1));
			snerticalMove1.Region = new(tileSize * new Vector2(3, 1), tileSize * new Vector2(1, 1));

			// add all the frames to the associated animations
			snakeFrames.AddFrame("Idle", snerticalIdle0);
			snakeFrames.AddFrame("Idle", snerticalIdle1);

			snakeFrames.AddFrame("Move", snerticalMove0);
			snakeFrames.AddFrame("Move", snerticalMove1);

			animatedSprite.Play("Idle");
		}

		if (startingSnakeDirection == StartingSnakeDirection.downOrLeft)
			direction *= -1;

		UpdateSpriteDirection(direction);

		base._Ready();

		// snakes avoid water
		blockingGround.AddRange( [ 
			"Water",
		]);

		// snakes avoid void
		blockingGround.AddRange(voidGround);
	}

	protected override void ResetMovementVariables()
	{
		triedOtherDirection = false;
		queueMove = false;
	}

	protected override bool AttemptMove(Vector2 newPosition, bool teleport = false, bool dryRun = false)
	{
		// only attempt to move if triggered by Cogito's movement or if the snake is already moving/animating
		if (queueMove || currentCharacterState == movingState || currentCharacterState == animatingState)
		{
			// where the tile is that the character will move to
			Vector2I newTilePosition = PositionToAtlasIndex(
				GetParent<Node2D>().ToGlobal(newPosition),
				gameManager.obstacleLayer
			);

			// turn the other direction if the new tile has a snake already on it; only checked if not teleporting
			if (!teleport && (!(newTilePosition.X >= 0 && newTilePosition.Y >= 0 && newTilePosition.X < screenTileDimensions.X && newTilePosition.Y < screenTileDimensions.Y)
				|| (gameManager.characterMatrix[newTilePosition.X, newTilePosition.Y] is Snake otherSnake
				&& otherSnake.currentCharacterState != deadState
				&& gameManager.characterMatrix[newTilePosition.X, newTilePosition.Y] != null
				&& gameManager.characterMatrix[newTilePosition.X, newTilePosition.Y] != this)))
			{
				if (!triedOtherDirection)
					return TryOtherDirection(newTilePosition - currentTileData.groundTile.position);
			
				queueMove = false;

				return false;
			}
	
			if (base.AttemptMove(newPosition, teleport, dryRun))
			{
				queueMove = false;
				return true;
			}
			else if (triedOtherDirection)
			{
				queueMove = false;
			}
		}

		return false;
	}

	/** <summary>When collided with another snake, turn around</summary> */
	protected override void OnCharacterCollision(Node2D body)
	{	
		// ensure the other snake is alive and collision didn't occur during an undo
		if (body is not Snake otherSnake || otherSnake == this || otherSnake.currentCharacterState == deadState 
			|| currentCharacterState == deadState || gameManager.cogito.undoOrRedoHappened || teleported || otherSnake.teleported)
		{
			return;
		}

		otherSnake.MoveBack();
	}

	/** <summary>Try to move 1 tile in the direction the conveyor is facing and adjust the snake direction accordingly</summary> */
	protected override void ConveyorInteraction()
	{
		// change the snakes direction if the conveyor's direction is on the snake's axis
		if (currentTileData.groundTile.direction.Abs() == direction.Abs())
			direction = currentTileData.groundTile.direction;

		base.ConveyorInteraction();
	}

	protected override void OnSuccessfulAttemptMove()
	{
		triedOtherDirection = false;
	}

	protected override bool TryOtherDirection(Vector2 movementDirection)
	{
		// don't try other direction on conveyor as it is forced in one direction
		if ((currentTileData.groundTile.customType == "Conveyor" || currentTileData.groundTile.customType == "EvilConveyor") 
			&& movementDirection != direction.Abs())
		{
			triedOtherDirection = true;
			return false;
		}

		direction *= -1;

		if (triedOtherDirection)
			return false;

		triedOtherDirection = true;
		
		Vector2 newPosition = Position + direction * movementDistance;

		return AttemptMove(newPosition);
	}

	public override void MoveBack()
	{
		base.MoveBack();
		direction *= -1;
	}

	protected override bool InputDetected(Vector2 inputDirection)
	{
		return inputDirection != Vector2.Zero;
	}

	/** <summary>Return the snake's direction when Cogito started a new move</summary> */
	public override Vector2 GetInputDirection()
	{
		if (queueMove)
		{
			return direction;
		}

		return Vector2.Zero;
	}

	/** <summary>Allow moving for snakes even if all other characters are idle if triggered by Cogito's movement</summary> */
	protected override bool OverrideAllCharactersIdleCheck()
	{
		return queueMove;
	}

	/** <summary>Snakes drown instantly in water</summary> */
	protected override void WaterInteraction()
	{
		StartDeath("Drown");
	}

	protected override void AddNewMovementDirection(MoveRecord previousMove)
	{
		CharacterMovement characterMovement;

		if (targetTileDifferenceVector == Vector2.Zero)
		{
			characterMovement = new((Vector2I)direction)
			{
				directionMoved = targetTileDifferenceVector
			};
		}
		else
		{
			characterMovement = new(targetTileDifferenceVector);
		}

		previousMove.movementDirections.Add(this, characterMovement);
	}
	
	/** <summary>Called at the end of death animation, sets snake to dead state</summary> */
	public override void Lose()
	{
		SetCharacterState(deadState);
	}
	
	public override void UpdateSpriteDirection(Vector2 movementDirection)
	{
		base.UpdateSpriteDirection(movementDirection);
		
		// flip sprite accordingly when moving vertically
		if (movementDirection.Y < 0)
		{
			animatedSprite.FlipH = false;
		}
		else if (movementDirection.Y > 0)
		{
			animatedSprite.FlipH = true;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
	}
}
