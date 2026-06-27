using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using static GameManager;
using System.Diagnostics;
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
	
	/** <summary>The current direction the snake will move in along its dedicated axis</summary> */
	public Vector2 direction = Vector2.Up;

	/** <summary>Flag that is set to true when the snake's turn was triggered by Cogito movement</summary> */
	public bool startMove = false,

		/** <summary>Flag that tracks if the other direction was tried for this movement turn</summary> */
		triedOtherDirection = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (snakeDirection == SnakeDirection.horizontal)
			direction = Vector2.Right;

		if (startingSnakeDirection == StartingSnakeDirection.downOrLeft)
			direction *= -1;

		UpdateSpriteDirection(direction);

		base._Ready();
		blockingGround.AddRange( [ 
			"", 
			"Void",
			"Water" 
		]);
	}

	protected override void MoveInit(Vector2 newPosition, bool teleport, bool dryRun, Vector2I newTilePosition)
	{
		triedOtherDirection = false;

		base.MoveInit(newPosition, teleport, dryRun, newTilePosition);
	}

	protected override bool AttemptMove(Vector2 newPosition, bool teleport = false, bool dryRun = false)
	{
		// only attempt to move if triggered by Cogito's movement or if the snake is already moving/animating
		if (startMove || currentCharacterState == CharacterState.moving || currentCharacterState == CharacterState.animating)
		{
			// where the tile is that the character will move to
			Vector2I newTilePosition = PositionToAtlasIndex(
				GetParent<Node2D>().ToGlobal(newPosition),
				gameManager.obstacleLayer
			);

			// turn the other direction if the new tile has a snake already on it
			if (gameManager.characterMatrix[newTilePosition.X, newTilePosition.Y] is Snake && gameManager.characterMatrix[newTilePosition.X, newTilePosition.Y] != null
				&& gameManager.characterMatrix[newTilePosition.X, newTilePosition.Y] != this)
			{
				if (!triedOtherDirection)
					return TryOtherDirection();
			
				startMove = false;

				return false;
			}
	
			if (base.AttemptMove(newPosition, teleport, dryRun))
			{
				startMove = false;
				return true;
			}
			else if (triedOtherDirection)
			{
				startMove = false;
			}
		}

		return false;
	}

	/** <summary>When collided with another snake, turn around</summary> */
	protected override void OnCharacterCollision(Node2D body)
	{		
		if (body is not Snake snake || snake == this || snake.currentCharacterState == CharacterState.dead || currentCharacterState == CharacterState.dead || gameManager.cogito.undoHappened)
			return;

		snake.MoveBack();
	}

	/** <summary>Try to move 1 tile in the direction the conveyor is facing and adjust the snake direction accordingly</summary> */
	protected override void ConveyorInteraction()
	{
		// change the snakes direction if the conveyor's direction is on the snake's access
		if (currentTileData.groundTile.direction.Abs() == direction.Abs())
			direction = currentTileData.groundTile.direction;

		base.ConveyorInteraction();
	}

	protected override bool TryOtherDirection()
	{
		// don't try other direction on conveyor as it is forced in one direction
		if (currentTileData.groundTile.customType == "Conveyor")
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
		if (startMove)
		{
			return direction;
		}

		return Vector2.Zero;
	}

	/** <summary>Allow moving for snakes even if all other characters are idle if triggered by Cogito's movement</summary> */
	protected override bool OverrideAllCharactersIdleCheck()
	{
		if (startMove)
			return true;
		
		return false;
	}

	/** <summary>Snakes drown instantly in water</summary> */
	protected override void WaterInteraction()
	{
		SetCharacterState(CharacterState.animating);
		StartDeath("Drown");
	}

	protected override void AddNewMovementDirection(PreviousMove previousMove)
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
		SetCharacterState(CharacterState.dead);
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