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
		idle,
		dead
	}

	/** <summary>tracks the instantiated falling sand object</summary> */
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

	/**<summary>List of all different types of void ground tiles</summary>*/
	protected readonly List<string> voidGround = 
	[
		"",
		"Void",
		"LeftToggleTileOff",
		"RightToggleTileOff"
	];
	
	/** <summary>flag that is true the moment the character's death is triggered (starts death animation)</summary> */
	public bool dying = false,

	/** <summary>true whenever the character is stopped mid-movement, usually from a collision with another character</summary> */
		stoppedMidMovement = false;

	/** <summary>List of all floor types that block movement, some characters will adjust this list</summary> */
	protected readonly List<string> blockingGround = [];

	/** <summary>Store the current data of the tiles the character is on</summary> */
	protected LayeredCustomTileData currentTileData;

	/** <summary>True if just teleported</summary> */
	protected bool teleported = false;

	/** <summary>The position to move to</summary> */
	private Vector2 targetPosition = Vector2.Zero;

	// round the target position to the exact center of the title to avoid shifting over time which could happen when undoing
	public Vector2 TargetPosition 
	{
		get { return targetPosition; }

		set
		{
			targetPosition.X = (tileSize * (float)Math.Floor(value.X / tileSize)) + (tileSize / 2);
			targetPosition.Y = (tileSize * (float)Math.Floor(value.Y / tileSize)) + (tileSize / 2);
		}
	}

	/** <summary>The difference vector from the original position to target in tile units, mainly used for logging teleports to undo</summary> */
	public Vector2I targetTileDifferenceVector = Vector2I.Zero;

	/** <summary>Distance the character moves to get to the next tile</summary> */
	protected float movementDistance = 0;

	/** <summary> The state that the character is currently in</summary> */
	public CharacterState currentCharacterState = CharacterState.idle,

	/** <summary>The target character state for the state transition</summary> */
		targetCharacterState = CharacterState.idle;

	/** <summary>The character state being exiting during the state</summary> */
	public CharacterState? exitCharacterState = null;

	/** <summary>Ran during start up</summary> */
	public override void _Ready()
	{
		// don't do anything if in level select
		if (gameManager.IsLevelSelect())
			return;

		// move 1 tile at a time by standard
		movementDistance = tileSize;

		// set initial tile data
		UpdateCurrentTileData();

		// call attempt move to trigger whatever tile the character spawned on if it has any effects
		AttemptMove(Position);
	}

	/** <summary>Set the finite state of the character</summary> */
	public void SetCharacterState(CharacterState newState)
	{
		CharacterState? oldState = currentCharacterState;

		// TODO: remember what can change the state back to the old state and comment
		// do not do anything if the new state is same as old unless an attempt to change states changed back to old state
		if (oldState == newState && newState != CharacterState.idle && !(newState == oldState && targetCharacterState != newState))
		{
			return;
		}

		targetCharacterState = newState;
		
		// do not exit the same state twice to handle state changes invoked mid-state transition
		if (exitCharacterState != oldState)
		{
			exitCharacterState = oldState;

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
				case CharacterState.dead:
					ExitDead();
					break;
			}
		}

		// reset exit character state once the old state was properly exited
		exitCharacterState = null;

		/* some exit methods change the state so if that happens do not even try entering the previous state
		 * and there is a case where the new state is the same as old IF attempting to exit/enter a different state forced back to the old */
		if (!(newState == oldState && newState == CharacterState.idle) && (newState != targetCharacterState || newState == oldState))
		{
			return;
		}

		currentCharacterState = targetCharacterState;

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
			case CharacterState.dead:
				EnterDead();
				break;
		}
	}

	/** <summary>Exit idle state</summary> */
	protected virtual void ExitIdle()
	{
		// reset some variables
		dying = false;
		stoppedMidMovement = false;
	}

	/** <summary>Exit moving state, handles stopping on a tile and checking what further actions are needed</summary> */
	public void ExitMoving()
	{
		// don't snap the position for collisions and being stopped by other means such as dying mid movement
		if (targetCharacterState != CharacterState.animating && !stoppedMidMovement)
		{
			// if a move was stopped prematurely (undoing) set the position backwards based on direction and target
			if ((Position - TargetPosition).Length() >= 2)
			{
				Position = TargetPosition - (targetTileDifferenceVector * tileSize);

				// reset to zero to avoid interactions like sliding on ice
				targetTileDifferenceVector = Vector2I.Zero;
			}
			else
			{
				// set position exactly to target
				Position = TargetPosition;
			}
		}

		PreviousMove previousMove = null;

		if (gameManager.previousMoves.Count > 0)
		{
			// get the previous data
			previousMove = gameManager.previousMoves.Pop();
		}

		LayeredCustomTileData[,] changedTiles = previousMove != null ? previousMove.changedTiles : new LayeredCustomTileData[20, 12];

		UpdateCurrentTileData();

		if (this is Cogito)
		{
			// log item position for undoing if found
			if (changedTiles[currentTileData.obstacleTile.position.X, currentTileData.obstacleTile.position.Y] == null &&
				(currentTileData.obstacleTile.customType == "Cog" || currentTileData.obstacleTile.customType == "Candy" || currentTileData.obstacleTile.customType == "Balloon"))
			{
				changedTiles[currentTileData.obstacleTile.position.X, currentTileData.obstacleTile.position.Y] = currentTileData;
			}
		}

		// if the move was already started with logged information, update the logged move
		if (previousMove != null)
		{
			UpdatePreviousMove(previousMove, changedTiles, false);
		}
		else if (targetTileDifferenceVector.Length() > 0)
		{
			// start logging a new set of moves if a distance was moved
			SaveNewMove(changedTiles);
		}

		// properly update counter if on water
		if (targetTileDifferenceVector.Length() > 0 || (gameManager.previousMoves.Count == 0 && gameManager.currentStamina == gameManager.maxStamina))
		{
			if (currentTileData.groundTile.customType == "Water")
			{
				WaterInteraction();
			}
			else
			{
				OutOfWaterInteraction();
			}
		}

		// interact with certain items
		if (currentTileData.obstacleTile.customType == "Cog")
		{
			CogInteraction();
		}
		else if (currentTileData.obstacleTile.customType == "Candy")
		{
			CandyInteraction();
		}
		else if (currentTileData.obstacleTile.customType == "Balloon")
		{
			BalloonInteraction();
		}

		// check goal interaction only if its activated
		if (currentTileData.groundTile.customType == "GoalOn")
		{
			GoalInteraction();
		}
		// if tile is void or null then make the character fall
		else if (voidGround.Contains(currentTileData.groundTile.customType) && !BalloonIsActive)
		{
			SetCharacterState(CharacterState.animating);
			StartDeath("Fall", .5f);
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
			ConveyorInteraction();
		}
		// if the tile is a teleporter or it is the first move and the character has moved and nothing is blocking the other teleporter, teleport
		else if (currentTileData.groundTile.customType == "Teleporter" && !(gameManager.previousMoves.Count > 0
			&& targetTileDifferenceVector == Vector2.Zero) && Teleport(true))
		{
			TelePop();

			animationPlayer.Play("Teleport");
			SetCharacterState(CharacterState.animating);
		}

		// update the tile data now that the turn has ended
		UpdateCurrentTileData();
	}

	/** <summary>Try to move 1 tile in the direction the conveyor is facing</summary> */
	protected virtual void ConveyorInteraction()
	{
		Vector2 newPosition = Position + currentTileData.groundTile.direction * movementDistance;
		AttemptMove(newPosition);
	}

	/** <summary>Specific interactions for when moving outside of water</summary> */
	protected virtual void OutOfWaterInteraction()
	{
		
	}

	/** <summary>Specific interactions when on water</summary> */
	protected virtual void WaterInteraction()
	{

	}

	/** <summary>Interaction with cog item</summary> */
	protected virtual void CogInteraction()
	{

	}

	/** <summary>Replace all original tiles of the specified atlas coordinates with the tile at the replacement atlas coordinates</summary> */
	protected void ReplaceTiles(Vector2I originalAtlasCoords, Vector2I replacementAtlasCoords)
	{
		var originalTilePositions = gameManager.groundLayer.GetUsedCellsById(1, originalAtlasCoords);

		foreach (Vector2I originalTilePosition in originalTilePositions)
		{
			gameManager.groundLayer.SetCell(
				originalTilePosition,
				1,
				replacementAtlasCoords
			);
		}
	}

	/** <summary>Start death animation and set dying to true</summary> */
	public virtual void StartDeath(string animationName, float animationSpeed = 1)
	{
		dying = true;

		// TODO: make more robust animation handler to ensure reset gets played between playing every animation
		animationPlayer.Play("RESET");
		animationPlayer.Play(animationName, customSpeed: animationSpeed);
	}

	/** <summary>Merge any new movements/changes with the previously logged move to update it properly</summary> */
	protected void UpdatePreviousMove(PreviousMove previousMove, LayeredCustomTileData[,] changedTiles, bool includeDirection)
	{
		gameManager.savedMove = gameManager.currentMove;
		
		if (includeDirection)
		{
			if (!previousMove.movementDirections.ContainsKey(this))
			{
				AddNewMovementDirection(previousMove);
			}
			else
			{
				previousMove.movementDirections[this].directionMoved += targetTileDifferenceVector;
			}
		}

		PreviousMove currentMove = new(gameManager.currentMove, changedTiles, previousMove.stamina,
			previousMove.candiesEaten, previousMove.balloonIsActive,
			movementDirections: previousMove.movementDirections, usedParadigmShift: previousMove.usedParadigmShift, leversToggled: previousMove.leversToggled);
		gameManager.previousMoves.Push(currentMove);
	}

	/** <summary>Add a new character's movement to the log for undoing</summary> */
	protected virtual void AddNewMovementDirection(PreviousMove previousMove)
	{
		previousMove.movementDirections.Add(this, new(targetTileDifferenceVector));
	}

	/** <summary>Handle balloons when teleporting</summary> */
	protected virtual void TelePop()
	{

	}
	
	/** <summary>Flag that's true while character has a working balloon</summary> */
	protected virtual bool BalloonIsActive { get { return false; } }

	/** <summary>Interaction with active goal tile</summary> */
	protected virtual void GoalInteraction()
	{

	}

	/** <summary>Interaction with candy tile</summary> */
	protected virtual void CandyInteraction()
	{

	}

	/** <summary>Start a new movement log for undoing</summary> */
	protected virtual void SaveNewMove(LayeredCustomTileData[,] changedTiles)
	{

	}

	/** <summary>Interact with balloon tile</summary> */
	protected virtual void BalloonInteraction()
	{

	}

	/** <summary>Exit animation state, ensuring the animation player is reset</summary> */
	public void ExitAnimating()
	{
		// only reset dying if the target state isn't the dead state
		if (targetCharacterState != CharacterState.dead)
			dying = false;

		Engine.TimeScale = 1;
		animationPlayer.Stop();
		animationPlayer.Play("RESET");
	}

	/** <summary>Enter the idle state</summary> */
	public void EnterIdle()
	{
		if (gameManager.AllCharactersIdle)
		{
			// once the character enters the idle state then the turn is completely done
			gameManager.currentMove++;
		}
		
		// set animation to idle
		SetSpriteAnimation("Idle");
	}

	/** <summary>Exit the dead state, resetting proper variables</summary> */
	public void ExitDead()
	{
		dying = false;
		stoppedMidMovement = false;
		Visible = true;
	}

	/** <summary>Enter the moving state</summary> */
	public void EnterMoving()
	{

	}

	/** <summary>Interaction with sand tiles</summary> */
	protected virtual void SandInteraction(LayeredCustomTileData[,] changedTiles)
	{

	}

	/** <summary>Handle popping the balloon</summary> */
	protected virtual void PopBalloon()
	{

	}

	/** <summary>Enter the animation state</summary> */
	public void EnterAnimating()
	{

	}

	/** <summary>Enter the dead state</summary> */
	public void EnterDead()
	{
		Visible = false;
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
		else if (BalloonIsActive && voidGround.Contains(currentTileData.groundTile.customType))
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

		return new(groundData, obstacleData, tilePos);
	}

	/** <summary>Return true if successfully moved</summary> */
	protected virtual bool AttemptMove(Vector2 newPosition, bool teleport = false, bool dryRun = false)
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

		// make character face direction of movement
		if (!dryRun)
		{
			teleported = false;

			UpdateSpriteDirection(movementDirection);
		}

		/* skip blocking checks if its rechecking the current tile to see if there is a new interaction to perform (such as a snake now being on void)
		 * don't move if the character will move to a blocking obstacle/floor 
		 * or if the character is on a conveyor and trying to move in the opposite direction
		 * or if the move would put the character out of bounds of the screen */
		if (movementDirection == Vector2.Zero ||
			(
				!blockingObstacles.Contains(newTileData.obstacleTile.customType) 
				&& (
					(!blockingGround.Contains(newTileData.groundTile.customType ?? "") && currentCharacterState != CharacterState.moving)
					^ currentCharacterState == CharacterState.moving
				)
				&& !(
					(currentTileData.groundTile.customType == "Conveyor" || currentTileData.groundTile.customType == "EvilConveyor")
					&& movementDirection == -1 * currentTileData.groundTile.direction
				)
				&& newTilePosition.X >= 0 && newTilePosition.Y >= 0 && newTilePosition.X < screenTileDimensions.X && newTilePosition.Y < screenTileDimensions.Y
			)
		)
		{
			MoveInit(newPosition, teleport, dryRun, newTilePosition);
			return true;
		}
		else if (!teleport)
		{
			// run try other direction if the character has logic for attempting to move the other way
			return TryOtherDirection();
		}

		return false;
	}

	/** <summary>called on collisions with other characters</summary> */
	protected virtual void OnCharacterCollision(Node2D body)
	{
		
	}

	/** <summary>Update the direction the sprite is facing based on the movement direction passed</summary> */
	public virtual void UpdateSpriteDirection(Vector2 movementDirection)
	{
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

	/** <summary>Try moving the other direction, returns true if this was successful</summary> */
	protected virtual bool TryOtherDirection()
	{
		return false;
	}
	
	/** <summary>Initialize the move</summary> */
	protected virtual void MoveInit(Vector2 newPosition, bool teleport, bool dryRun, Vector2I newTilePosition)
	{
		// simply return true if a dry run to skip actually moving the character
		if (dryRun)
			return;

		// ensure proper data when starting a move
		UpdateCurrentTileData();

		teleported = teleport;
		TargetPosition = newPosition;
		targetTileDifferenceVector = newTilePosition - currentTileData.groundTile.position;

		if (teleported)
			return;

		LayeredCustomTileData targetTileData = GetTileCustomType(newTilePosition, gameManager.groundLayer,
			gameManager.obstacleLayer);

		UpdateAnimation(targetTileData);

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

		if (voidGround.Contains(currentTileData.groundTile.customType) && BalloonIsActive && targetTileDifferenceVector.Length() > 0)
		{
			PopBalloon();
		}

		SetCharacterState(CharacterState.moving);
	}

	/** <summary>Update the characters animation based on the tiles they are interacting with</summary> */
	private void UpdateAnimation(LayeredCustomTileData targetTileData)
	{
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

	/** <summary>Get the direction of the input</summary> */
	public virtual Vector2 GetInputDirection()
	{
		return Vector2.Zero;
	}

	/** <summary>Called at the end of the paradigm shift animation</summary> */
	public void EndParadigmShiftAnimation()
	{
		SetCharacterState(CharacterState.idle);
		AttemptMove(Position, false, false);
	}

	/** <summary>Update the tile data the character is on</summary> */
	public void UpdateCurrentTileData()
	{
		if (currentTileData is not null && gameManager.characterMatrix[currentTileData.tilePosition.X, currentTileData.tilePosition.Y] == this)
		{
			gameManager.characterMatrix[currentTileData.tilePosition.X, currentTileData.tilePosition.Y] = null;
		}
		
		// convert position to tile positions
		Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, gameManager.obstacleLayer);

		// identify what the character is currently standing on
		currentTileData = GetTileCustomType(currentTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);

		gameManager.characterMatrix[currentTilePosition.X, currentTilePosition.Y] = this;
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
			if ((Position - TargetPosition).Length() >= 2)
			{
				return false;
			}
			// stop moving when close enough to target position and the character is still moving (may stop after moving?)
			else if (currentCharacterState == CharacterState.moving)
			{
				SetCharacterState(CharacterState.idle);
			}
		}

		return true;
	}

	/** <summary>Set the buffered input which represents inputs that occur mid movement/animation</summary> */
	protected virtual void SetBufferedInput()
	{

	}

	/** <summary>Runs every physics frame</summary> */
	public override void _PhysicsProcess(double delta)
	{
		if (gameManager.IsLevelSelect())
			return;

		ProcessBeforePauseCheck(delta);

		// don't allow controlling character while game is paused
		if (Engine.TimeScale == 0)
			return;

		// don't allow controlling character while dying but allow resetting
		if (currentCharacterState == CharacterState.animating || currentCharacterState == CharacterState.dead)
			return;

		if (!UpdateMove() || (!gameManager.AllCharactersIdle && !OverrideAllCharactersIdleCheck()))
			return;

		Vector2 inputDirection = GetInputDirection();

		// i'm sammyrog
		if (InputDetected(inputDirection))
		{
			// where the character will move
			Vector2 newPosition = Position + inputDirection * movementDistance;

			if (this is not Cogito && gameManager.cogito.targetTileDifferenceVector.Length() == 0)
				newPosition = Position;

			AttemptMove(newPosition);
		}
		else if (MoveWithBuffer) { }
		// don't allow paradigm shifting if none are remaining
		else CheckParadigmShiftInput();
	}

	/** <summary>This override allows for the character to continue processing if all characters are not idle</summary> */
	protected virtual bool OverrideAllCharactersIdleCheck()
	{
		return false;
	}

	/** <summary>Check if paradigm shift was pressed and handle accordingly</summary> */
	protected virtual void CheckParadigmShiftInput()
	{

	}

	/** <summary>general actions to perform before checking for the pause input</summary> */
	protected virtual void ProcessBeforePauseCheck(double delta)
	{

	}

	/** <summary>Return true if an input was detected</summary> */
	protected virtual bool InputDetected(Vector2 inputDirection) 
	{ 
		return false; 
	}

	/** <summary>Move back to the tile the character came from, called mid move</summary> */
	public virtual void MoveBack()
	{
		PreviousMove previousMove = gameManager.previousMoves.Pop();
		targetTileDifferenceVector *= -1;
		UpdatePreviousMove(previousMove, previousMove.changedTiles, true);
		// targetTileDifferenceVector *= -1;
		TargetPosition += targetTileDifferenceVector * tileSize;
	
		UpdateSpriteDirection(targetTileDifferenceVector);
	}

	/** <summary>This property is called to try to move with the buffered input for certain characters, returns true if successfully moved</summary> */
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
					Position = TargetPosition;
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