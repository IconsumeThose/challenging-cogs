using Godot;
using System;
using System.Collections.Generic;
using static GameManager;
public partial class Character : CharacterBody2D
{	
	/** <summary> the state the character is in </summary> */
	public enum CharacterState
	{
		moving,
		animating,
		idle
	}

	protected Node2D fallingSand = null;

	/** <summary>The distance the character moves every time  </summary> */
	[Export] public int tileSize = 32;

	/** <summary>The screen dimensions in tiles that the character is in</summary> */
	[Export] public Vector2I screenTileDimensions = new(20, 11);

	/** <summary>The speed in which the character moves</summary> */
	[Export] public float movementSpeed = 150;

	/** <summary>Reference to the game manager in the current scene</summary> */
	[Export] protected GameManager gameManager;

	/** <summary>Reference to the character's main animated sprite 2D</summary> */
	[Export]
	public AnimatedSprite2D animatedSprite;

	/** <summary>Reference to </summary> */
	[Export] public AnimationPlayer animationPlayer;

	/** <summary>List of all obstacles that block movement</summary> */
	protected readonly List<string> blockingObstacles =
	[
		"Rock",
		"CogCrystal",
		"ReinforcedCogCrystal",
		"DeinforcedCogCrystal",
		"LeverLeft",
		"LeverRight"
	];

	/** <summary>Store the current data of the tiles the character is on</summary> */
	protected LayeredCustomTileData currentTileData;

	/** <summary>True if just teleported</summary> */
	protected bool teleported = false;

	/** <summary>The position to move to</summary> */
	protected Vector2 targetPosition = Vector2.Zero;

	/** <summary>The difference vector from the original position to target in tile units, mainly used for logging teleports to undo</summary> */
	protected Vector2I targetTileDifferenceVector = Vector2I.Zero;

	/** <summary>Distance the character moves to get to the next tile</summary> */
	protected float movementDistance = 0;

	/** <summary> The state that the character is currently in</summary> */
	public CharacterState currentCharacterState = CharacterState.idle;

	/** <summary>Ran during start up</summary> */
	public override void _Ready()
	{
		// don't do anything if in level select
		if (gameManager.IsLevelSelect())
			return;

		movementDistance = tileSize;

		gameManager.currentStamina = gameManager.maxStamina;

		// convert position to tile positions
		Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, gameManager.obstacleLayer);

		// identify what the character is currently standing on
		currentTileData = GetTileCustomType(currentTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);

		AttemptMove(Position);
	}

	/** <summary>Set the finite state of the character</summary> */
	public void SetCharacterState(CharacterState newState)
	{
		CharacterState oldState = currentCharacterState;

		if (oldState == newState && newState != CharacterState.idle)
		{
			return;
		}

		currentCharacterState = newState;

		switch (oldState)
		{
			case CharacterState.idle:
				ExitIdle();
				break;
			case CharacterState.moving:
				ExitMoving();
				break;
			case CharacterState.animating:
				ExitAnimating();
				break;
		}

		// some exit methods change the state so if that happens do not even try entering the previous state
		if (newState != currentCharacterState)
		{
			return;
		}

		switch (currentCharacterState)
		{
			case CharacterState.idle:
				EnterIdle();
				break;
			case CharacterState.moving:
				EnterMoving();
				break;
			case CharacterState.animating:
				EnterAnimating();
				break;
		}
	}

	/** <summary>Exit idle state</summary> */
	public void ExitIdle()
	{

	}

	/** <summary>Exit moving state, handles stopping on a tile and checking what further actions are needed</summary> */
	public void ExitMoving()
	{
		// if a move was stopped prematurely (undoing) set the position backwards based on direction and target
		if ((Position - targetPosition).Length() >= 2)
		{
			Position = targetPosition - (targetTileDifferenceVector * tileSize);

			// reset to zero to avoid interactions like sliding on ice
			targetTileDifferenceVector = Vector2I.Zero;
		}
		else
		{
			// set position exactly to target
			Position = targetPosition;
		}

		PreviousMove previousMove = null;

		if (gameManager.currentMove == gameManager.savedMove && gameManager.previousMoves.Count > 0)
		{
			// get the previous data
			previousMove = gameManager.previousMoves.Pop();
		}

		LayeredCustomTileData[,] changedTiles = previousMove != null ? previousMove.changedTiles : new LayeredCustomTileData[20, 12];

		UpdateCurrentTileData();

		// set cog position if found
		if (changedTiles[currentTileData.obstacleTile.position.X, currentTileData.obstacleTile.position.Y] == null &&
			(currentTileData.obstacleTile.customType == "Cog" || currentTileData.obstacleTile.customType == "Candy" || currentTileData.obstacleTile.customType == "Balloon"))
		{
			changedTiles[currentTileData.obstacleTile.position.X, currentTileData.obstacleTile.position.Y] = currentTileData;
		}

		// if the previous move was forced (ice, conveyor, etc...) then merge the previous moves data with the current one
		if (previousMove != null)
		{
			UpdatePreviousMove(previousMove, changedTiles, false);
		}
		else if (targetTileDifferenceVector.Length() > 0)
		{
			SaveNewMove(changedTiles);
		}

		// properly update counter if on water
		if (targetTileDifferenceVector.Length() > 0 || (gameManager.previousMoves.Count == 0 && gameManager.currentStamina == gameManager.maxStamina))
		{
			if (currentTileData.groundTile.customType == "Water")
			{
				gameManager.StaminaChanged(1, this);
			}
			else
			{
				gameManager.StaminaChanged(-99999999, this);
			}
		}

		// collect the cog if its on the same tile as the character
		if (currentTileData.obstacleTile.customType == "Cog")
		{
			gameManager.CogChallenged(1);
			gameManager.obstacleLayer.SetCell(currentTileData.groundTile.position);
		}
		else if (currentTileData.obstacleTile.customType == "Candy")
		{
			CandyInteraction();
		}
		else if (currentTileData.obstacleTile.customType == "Balloon")
		{
			BalloonInteraction();
		}

		// win the game if on the goal while it is activated
		if (currentTileData.groundTile.customType == "GoalOn")
		{
			GoalInteraction();
		}
		// if tile is void or null then make the character fall
		else if ((currentTileData.groundTile.customType ?? "Void") == "Void" && !BalloonIsActive)
		{
			SetCharacterState(CharacterState.animating);
			animationPlayer.Play("Fall", customSpeed: 0.5f);
		}
		// if tile is ice, make the character continue moving in the same direction they were moving
		else if (currentTileData.groundTile.customType == "Ice" && targetTileDifferenceVector.Length() > 0)
		{
			Vector2 newPosition = Position + ((Vector2)targetTileDifferenceVector).Normalized() * movementDistance;
			AttemptMove(newPosition);
		}
		// if tile is conveyor, make the character move in the direction the conveyor is facing
		else if (currentTileData.groundTile.customType == "Conveyor" || currentTileData.groundTile.customType == "EvilConveyor")
		{
			Vector2 newPosition = Position + currentTileData.groundTile.direction * movementDistance;
			AttemptMove(newPosition);
		}
		// if the tile is a teleporter or it is the first move and the character has moved and nothing is blocked on the other teleporter, move
		else if (currentTileData.groundTile.customType == "Teleporter" && !(gameManager.previousMoves.Count > 0
			&& targetTileDifferenceVector == Vector2.Zero) && Teleport(true))
		{
			TelePop();

			animationPlayer.Play("Teleport");
			SetCharacterState(CharacterState.animating);
		}

		UpdateCurrentTileData();
	}

	private void UpdatePreviousMove(PreviousMove previousMove, LayeredCustomTileData[,] changedTiles, bool includeDirection)
	{
		if (includeDirection)
		{
			if (!previousMove.movementDirections.ContainsKey(this))
				previousMove.movementDirections.Add(this, Vector2I.Zero);
				
			previousMove.movementDirections[this] += targetTileDifferenceVector;
		}


		PreviousMove currentMove = new(gameManager.currentMove, changedTiles, previousMove.stamina,
			 previousMove.candiesEaten, previousMove.balloonIsActive,
			movementDirections: previousMove.movementDirections, usedParadigmShift: previousMove.usedParadigmShift, leversToggled: previousMove.leversToggled);
		gameManager.previousMoves.Push(currentMove);
	}

	protected virtual void TelePop()
	{

	}

	protected virtual bool BalloonIsActive { get { return false; } }
	protected virtual void GoalInteraction()
	{

	}

	protected virtual void CandyInteraction()
	{

	}

	protected virtual void SaveNewMove(LayeredCustomTileData[,] changedTiles)
	{
		gameManager.savedMove = gameManager.currentMove;
		PreviousMove currentMove = new(gameManager.currentMove, changedTiles, gameManager.currentStamina, 0, false,
			movementDirections: new() { 
				{ this, targetTileDifferenceVector } 
			}
		);

		gameManager.previousMoves.Push(currentMove);
	}

	protected virtual void BalloonInteraction()
	{

	}

	/** <summary>Exit animation state, ensuring the animation player is reset</summary> */
	public void ExitAnimating()
	{
		Engine.TimeScale = 1;
		animationPlayer.Stop();
		animationPlayer.Play("RESET");
	}

	/** <summary>Enter the idle state</summary> */
	public void EnterIdle()
	{
		// once the character enters the idle state then the turn is completely done
		gameManager.currentMove++;
		
		// set animation to idle
		SetSpriteAnimation("Idle");
	}

	/** <summary>Enter the moving state, prepares the character for moving and starting the correct animations</summary> */
	public void EnterMoving()
	{
		// where the tile is that the character will move to
		Vector2I newTilePosition = PositionToAtlasIndex(
			GetParent<Node2D>().ToGlobal(targetPosition),
			gameManager.obstacleLayer
		);

		LayeredCustomTileData targetTileData = GetTileCustomType(newTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);

		// set animation accordingly to the current tile
		if ((currentTileData.groundTile.customType == "Conveyor" || currentTileData.groundTile.customType == "EvilConveyor") && gameManager.savedMove == gameManager.currentMove)
		{
			SetSpriteAnimation("Idle");
		}
		else if (currentTileData.groundTile.customType == "Ice")
		{
			SetSpriteAnimation("Slide");
		}
		else if (currentTileData.groundTile.customType == "Water")
		{
			if (targetTileData.groundTile.customType == "Water")
			{
				SetSpriteAnimation("SwimIdle");
			}
			else
			{
				SetSpriteAnimation("Move");
			}
		}
		else
		{
			SetSpriteAnimation("Move");
		}

		PreviousMove previousMove = null;

		if (gameManager.savedMove == gameManager.currentMove && gameManager.previousMoves.Count > 0)
		{
			// get the previous data
			previousMove = gameManager.previousMoves.Pop();
		}

		LayeredCustomTileData[,] changedTiles = previousMove != null ? previousMove.changedTiles : new LayeredCustomTileData[20, 12];


		if (currentTileData.groundTile.customType == "Sand" && targetTileDifferenceVector.Length() > 0)
		{
			SandInteraction(changedTiles);
		}

		// if the previous move was forced (ice, conveyor, etc...) then merge the previous moves data with the current one
		if (previousMove != null)
		{
			UpdatePreviousMove(previousMove, changedTiles, true);
		}
		else if (targetTileDifferenceVector.Length() > 0)
		{
			SaveNewMove(changedTiles);
		}

		if ((currentTileData.groundTile.customType ?? "Void") == "Void" && BalloonIsActive && targetTileDifferenceVector.Length() > 0)
		{
			PopBalloon();
		}
	}

	protected virtual void SandInteraction(LayeredCustomTileData[,] changedTiles)
	{

	}

	protected virtual void PopBalloon()
	{

	}

	/** <summary>Enter the animation state</summary> */
	public void EnterAnimating()
	{

	}

	/** <summary>Convert global position to the tile position at the specified tile map</summary> */
	public static Vector2I PositionToAtlasIndex(Vector2 position, TileMapLayer tileMap)
	{
		return tileMap.LocalToMap(tileMap.ToLocal(position));
	}

	/** <summary>Only set the animation if its different to possibly avoid resetting the animation</summary> */
	public void SetSpriteAnimation(string animationName, float speed = 1)
	{
		// switch to the water variant of the animation if applicable
		if (this is Cogito && gameManager.currentStamina != gameManager.maxStamina)
		{
			switch (animationName)
			{
				case "Idle":
					animationName = "SwimIdle";
					break;
				case "ParadigmShift":
					animationName = "SwimParadigmShift";
					break;
			}
		}
		// switch to float variant of the animation if applicable
		else if (BalloonIsActive && (currentTileData.groundTile.customType ?? "Void") == "Void")
		{
			switch (animationName)
			{
				case "Move":
				case "Idle":
					animationName = "Float";
					break;
				case "ParadigmShift":
					animationName = "FloatParadigmShift";
					break;
			}
		}

		if (animatedSprite.Animation != animationName)
		{
			animatedSprite.Play(animationName, speed);
		}
	}

	/** <summary>Get the tile types at the specified position</summary> */
	public static LayeredCustomTileData GetTileCustomType(Vector2I tilePos, TileMapLayer groundLayer, TileMapLayer obstacleLayer)
	{
		CustomTileData groundData = new(groundLayer.GetCellTileData(tilePos), tilePos, groundLayer);

		CustomTileData obstacleData = new(obstacleLayer.GetCellTileData(tilePos), tilePos, obstacleLayer);

		return new(groundData, obstacleData);
	}

	/** <summary>Return true if successfully moved</summary> */
	protected bool AttemptMove(Vector2 newPosition, bool teleport = false, bool dryRun = false)
	{
		// where the tile is that the character will move to
		Vector2I newTilePosition = PositionToAtlasIndex(
			GetParent<Node2D>().ToGlobal(newPosition),
			gameManager.obstacleLayer
		);

		// the type of tile the character will move to
		LayeredCustomTileData newTileData = GetTileCustomType(newTilePosition, gameManager.groundLayer,
			gameManager.obstacleLayer);

		Vector2 movementDirection = (newPosition - Position).Normalized();

		if (!dryRun)
		{
			teleported = false;

			// flip sprite accordingly when moving horizontally
			if (movementDirection.X > 0)
			{
				animatedSprite.FlipH = false;
			}
			else if (movementDirection.X < 0)
			{
				animatedSprite.FlipH = true;
			}
		}

		// don't move if the character will move to a blocking obstacle
		if (!blockingObstacles.Contains(newTileData.obstacleTile.customType)
			&& !((currentTileData.groundTile.customType == "Conveyor" || currentTileData.groundTile.customType == "EvilConveyor")
			&& movementDirection == -1 * currentTileData.groundTile.direction)
			&& newTilePosition.X >= 0 && newTilePosition.Y >= 0 && newTilePosition.X < screenTileDimensions.X && newTilePosition.Y < screenTileDimensions.Y)
		{
			MoveInit(newPosition, teleport, dryRun, newTilePosition);
			return true;
		}

		return false;
	}

	protected virtual void MoveInit(Vector2 newPosition, bool teleport, bool dryRun, Vector2I newTilePosition)
	{
		// simply return true if a dry run to skip actually moving the character
		if (dryRun)
			return;

		teleported = teleport;
		targetPosition = newPosition;
		targetTileDifferenceVector = newTilePosition - currentTileData.groundTile.position;

		if (!teleported)
			SetCharacterState(CharacterState.moving);
	}

	/** <summary>Toggle FlipH to make the sprite look the opposite way, called from animation player</summary> */
	public void ToggleSpriteFlipH()
	{
		animatedSprite.FlipH = !animatedSprite.FlipH;
	}

	/** <summary>Open the lose menu, called from animation player</summary> */
	public virtual void Lose()
	{

	}

	/** <summary>Get the direction of the input, not allowing for diagonal inputs</summary> */
	public virtual Vector2 GetInputDirection()
	{
		return Vector2.Zero;
	}

	/** <summary>Called at the end of the paradigm shift animation</summary> */
	public void EndParadigmShiftAnimation()
	{
		AttemptMove(Position);
		SetCharacterState(CharacterState.idle);
	}

	/** <summary>Update the tile data the character is on</summary> */
	public void UpdateCurrentTileData()
	{
		// convert position to tile positions
		Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, gameManager.obstacleLayer);

		// identify what the character is currently standing on
		currentTileData = GetTileCustomType(currentTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);
	}

	/** <summary>Run every frame to update movement and check what happens when the character stops</summary> */
	public bool UpdateMove()
	{
		// move the character to target position
		if (currentCharacterState == CharacterState.moving)
		{
			SetBufferedInput();

			Velocity = ((Vector2)targetTileDifferenceVector).Normalized() * movementSpeed;

			MoveAndSlide();

			// skip checking movement inputs if the character is too far from target destination
			if ((Position - targetPosition).Length() >= 2)
			{
				return false;
			}
			// stop moving when close enough to target position
			else
			{
				SetCharacterState(CharacterState.idle);
			}
		}

		return true;
	}

	protected virtual void SetBufferedInput()
	{

	}

	/** <summary>Runs every physics frame</summary> */
	public override void _PhysicsProcess(double delta)
	{
		if (gameManager.IsLevelSelect())
			return;

		ProcessBeforePauseCheck();

		// don't allow controlling character while game is paused
		if (Engine.TimeScale == 0)
			return;

		ProcessAfterPauseCheck(delta);

		// don't allow controlling character while dying but allow resetting
		if (currentCharacterState == CharacterState.animating)
			return;

		if (!UpdateMove() || !gameManager.AllCharactersIdle)
			return;

		Vector2 inputDirection = GetInputDirection();

		// i'm sammyrog
		if (InputDetected(inputDirection))
		{
			// where the character will move
			Vector2 newPosition = Position + inputDirection * movementDistance;

			AttemptMove(newPosition);
		}
		else if (MoveWithBuffer) { }
		// don't allow paradigm shifting if none are remaining
		else CheckParadigmShiftInput();
	}

	protected virtual void CheckParadigmShiftInput()
	{

	}

	protected virtual void ProcessBeforePauseCheck()
	{

	}

	protected virtual void ProcessAfterPauseCheck(double delta)
	{

	}

	protected virtual bool InputDetected(Vector2 inputDirection) 
	{ 
		return false; 
	}

	protected virtual bool MoveWithBuffer { get { return false; } }
	
	/** <summary>Returns true if successfully teleported, dry run merely checks if its possible to teleport but doesn't actually teleport</summary> */
	public bool Teleport(bool dryRun = false)
	{
		// check the atlas positions to see if its the same exact teleporter type
		Vector2I teleporterAtlasPosition = gameManager.groundLayer.GetCellAtlasCoords(currentTileData.groundTile.position),
			previousTileAtlasPosition = gameManager.groundLayer.GetCellAtlasCoords(currentTileData.groundTile.position - targetTileDifferenceVector);

		// find all instances of this teleport
		var teleporters = gameManager.groundLayer.GetUsedCellsById(1, teleporterAtlasPosition);

		foreach (Vector2I teleporterPosition in teleporters)
		{
			// teleport if its not the teleporter the character is currently on and the character didn't just come from a teleporter
			if (teleporterPosition != currentTileData.groundTile.position && !(teleporterAtlasPosition == previousTileAtlasPosition && teleported))
			{
				Vector2 teleporterPositionDifference = (Vector2)(teleporterPosition - currentTileData.groundTile.position) * movementDistance;

				if (dryRun)
					return AttemptMove(Position + teleporterPositionDifference, true, true);

				// only actually move and return true if successfully teleported
				if (AttemptMove(Position + teleporterPositionDifference, true))
				{
					// instantly tp to destination if teleporting
					Position = targetPosition;
					UpdateCurrentTileData();

					if (gameManager.previousMoves.Count > 0)
					{
						PreviousMove previousMove = gameManager.previousMoves.Pop();

						// use previous data with added direction
						UpdatePreviousMove(previousMove, previousMove.changedTiles, true);
					}

					targetTileDifferenceVector = new(0, 0);

					return true;
				}
			}
		}

		return false;
	}
}
