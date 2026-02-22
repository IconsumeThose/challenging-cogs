using Godot;
using System.Collections.Generic;
using System;

public partial class Cogito : CharacterBody2D
{
	/** <summary>
		Class <c>PreviousMove</c> keeps track of all relevant information for a move so that it can be undone
		</summary> */
	public class PreviousMove(LayeredCustomTileData[,] changedTiles, int stamina, int candiesEaten,  bool balloonIsActive, Vector2I? movementDirection = null, bool usedParadigmShift = false, bool leversToggled = false)
	{
		public readonly LayeredCustomTileData[,] changedTiles = changedTiles ?? new LayeredCustomTileData[20, 12];

		/** <summary> The direction that Cogito moved</summary> */
		public Vector2I? movementDirection = movementDirection ?? Vector2I.Zero;
		public bool usedParadigmShift = usedParadigmShift;
		public bool leversToggled = leversToggled;
		public int stamina = stamina;
		public int candiesEaten = candiesEaten;
		public bool balloonIsActive = balloonIsActive;
	}

	/** <summary>stack of all previous moves so that they can be undone in correct order(LIFO)</summary> */
	public readonly Stack<PreviousMove> previousMoves = new();

	/** <summary> the state Cogito is in </summary> */
	public enum CogitoState
	{
		moving,
		animating,
		idle
	}

	private Node2D fallingSand = null;

	/** <summary>The distance Cogito moves every time  </summary> */
	[Export] public int tileSize = 32;

	/** <summary>The screen dimensions in tiles that Cogito is in</summary> */
	[Export] public Vector2I screenTileDimensions = new(20, 11);

	/** <summary>The speed in which Cogito moves</summary> */
	[Export] public float movementSpeed = 150;

	/** <summary>Setting to allow holding down a direction to keep moving in that direction</summary> */
	[Export] public bool holdToMove = true;

	/** <summary>Reference to the game manager in the current scene</summary> */
	[Export] GameManager gameManager;

	[Export]
	/** <summary>Reference to win menu in scene</summary> */
	public Menu winMenu,

		/** <summary>Reference to pause menu in scene</summary> */
		pauseMenu,

		/** <summary>Reference to lose menu in scene</summary> */
		loseMenu;

	/** <summary>Reference to Cogito's main animated sprite 2D</summary> */
	[Export] public AnimatedSprite2D animatedSprite,
		balloonSprite;

	/** <summary>Reference to </summary> */
	[Export] public AnimationPlayer animationPlayer;

	/** <summary>Used for the falling sand animation</summary> */
	[Export] public PackedScene fallingSandScene;

	/** <summary>True when levers are facing left</summary> */
	public bool leversAreFacingLeft = true,
		balloonIsActive = false;

	/** <summary>List of all obstacles that block movement</summary> */
	private readonly List<string> blockingObstacles =
	[
		"Rock",
		"CogCrystal",
		"ReinforcedCogCrystal",
		"DeinforcedCogCrystal",
		"LeverLeft",
		"LeverRight"
	];

	/** <summary>Store the input direction to be buffered</summary> */
	private Vector2 bufferedInput = Vector2.Zero;

	/** <summary>Store the current data of the tiles Cogito is on</summary> */
	private LayeredCustomTileData currentTileData;

	/** <summary>True if the move was forced by a special tile, needed for keeping track of moves to undo</summary> */
	private bool mergeNextMove = false,

		/** <summary>True if just teleported</summary> */
		teleported = false;

	/** <summary>The position to move to</summary> */
	private Vector2 targetPosition = Vector2.Zero;

	/** <summary>The difference vector from the original position to target in tile units, mainly used for logging teleports to undo</summary> */
	private Vector2I targetTileDifferenceVector = Vector2I.Zero;

	/** <summary>Distance Cogito moves to get to the next tile</summary> */
	private float movementDistance = 0;

	private int candiesEaten = 0;

	/** <summary> The state that Cogito is currently in</summary> */
	public CogitoState currentCogitoState = CogitoState.idle;

	/** <summary>Set the finite state of Cogito</summary> */
	public void SetCogitoState(CogitoState newState)
	{
		CogitoState oldState = currentCogitoState;

		if (oldState == newState && newState != CogitoState.idle)
		{
			return;
		}

		currentCogitoState = newState;

		switch (oldState)
		{
			case CogitoState.idle:
				ExitIdle();
				break;
			case CogitoState.moving:
				ExitMoving();
				break;
			case CogitoState.animating:
				ExitAnimating();
				break;
		}

		// some exit methods change the state so if that happens do not even try entering the previous state
		if (newState != currentCogitoState)
		{
			return;
		}

		switch (currentCogitoState)
		{
			case CogitoState.idle:
				EnterIdle();
				break;
			case CogitoState.moving:
				EnterMoving();
				break;
			case CogitoState.animating:
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

		if (mergeNextMove && previousMoves.Count > 0)
		{
			// get the previous data
			previousMove = previousMoves.Pop();
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
			// use previous data with added direction
			PreviousMove currentMove = new(changedTiles, previousMove.stamina, previousMove.candiesEaten, previousMove.balloonIsActive,
				movementDirection: previousMove.movementDirection,
				usedParadigmShift: previousMove.usedParadigmShift, leversToggled: previousMove.leversToggled);
			previousMoves.Push(currentMove);
		}
		else if (targetTileDifferenceVector.Length() > 0)
		{
			PreviousMove currentMove = new(changedTiles, gameManager.currentStamina, candiesEaten, balloonIsActive,
				movementDirection: targetTileDifferenceVector);
			previousMoves.Push(currentMove);
		}

		// properly update counter if on water
		if (targetTileDifferenceVector.Length() > 0 || (previousMoves.Count == 0 && gameManager.currentStamina == gameManager.maxStamina))
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

		mergeNextMove = false;

		// collect the cog if its on the same tile as Cogito
		if (currentTileData.obstacleTile.customType == "Cog")
		{
			gameManager.CogChallenged(1);
			gameManager.obstacleLayer.SetCell(currentTileData.groundTile.position);
		}
		else if (currentTileData.obstacleTile.customType == "Candy")
		{
			candiesEaten++;
			gameManager.obstacleLayer.SetCell(currentTileData.groundTile.position);
		}
		else if (currentTileData.obstacleTile.customType == "Balloon" && !balloonIsActive)
		{
			balloonIsActive = true;
			balloonSprite.Visible = true;
			balloonSprite.Animation = "default";
			gameManager.obstacleLayer.SetCell(currentTileData.groundTile.position);
			SetSpriteAnimation("Float");
		}

		// win the game if on the goal while it is activated
		if (currentTileData.groundTile.customType == "GoalOn")
		{
			Engine.TimeScale = 0;
			winMenu.Visible = true;
			winMenu.GetNode<Button>("VBoxContainer/NextLevelButton").GrabFocus();

			DataManager.SaveGame();
		}
		// if tile is void or null then make Cogito fall
		else if ((currentTileData.groundTile.customType ?? "Void") == "Void" && !balloonIsActive)
		{
			SetCogitoState(CogitoState.animating);
			animationPlayer.Play("Fall", customSpeed: 0.5f);
		}
		// if tile is ice, make Cogito continue moving in the same direction they were moving
		else if (currentTileData.groundTile.customType == "Ice" && targetTileDifferenceVector.Length() > 0)
		{
			Vector2 newPosition = Position + ((Vector2)targetTileDifferenceVector).Normalized() * movementDistance;
			mergeNextMove = true;
			mergeNextMove = AttemptMove(newPosition);
		}
		// if tile is conveyor, make Cogito move in the direction the conveyor is facing
		else if (currentTileData.groundTile.customType == "Conveyor" || currentTileData.groundTile.customType == "EvilConveyor")
		{
			Vector2 newPosition = Position + currentTileData.groundTile.direction * movementDistance;
			mergeNextMove = true;
			mergeNextMove = AttemptMove(newPosition);
		}
		// if the tile is a teleporter or it is the first move and Cogito has moved and nothing is blocked on the other teleporter, move
		else if (currentTileData.groundTile.customType == "Teleporter" && !(previousMoves.Count > 0
			&& targetTileDifferenceVector == Vector2.Zero) && Teleport(true))
		{
			if (balloonIsActive)
			{
				balloonIsActive = false;
				balloonSprite.Play("pop");
			}

			animationPlayer.Play("Teleport");
			SetCogitoState(CogitoState.animating);
		}

		UpdateCurrentTileData();
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
		// set animation to idle
		SetSpriteAnimation("Idle");
	}

	/** <summary>Enter the moving state, prepares Cogito for moving and starting the correct animations</summary> */
	public void EnterMoving()
	{
		// where the tile is that Cogito will move to
		Vector2I newTilePosition = PositionToAtlasIndex(
			GetParent<Node2D>().ToGlobal(targetPosition),
			gameManager.obstacleLayer
		);

		LayeredCustomTileData targetTileData = GetTileCustomType(newTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);

		// set animation accordingly to the current tile
		if ((currentTileData.groundTile.customType == "Conveyor" || currentTileData.groundTile.customType == "EvilConveyor") && mergeNextMove)
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

			if (mergeNextMove && previousMoves.Count > 0)
			{
				// get the previous data
				previousMove = previousMoves.Pop();
			}

			LayeredCustomTileData[,] changedTiles = previousMove != null ? previousMove.changedTiles : new LayeredCustomTileData[20, 12];


		if (currentTileData.groundTile.customType == "Sand" && targetTileDifferenceVector.Length() > 0)
		{
			changedTiles[currentTileData.groundTile.position.X, currentTileData.groundTile.position.Y] = currentTileData;

			// make sand fall after walking off that tile
			gameManager.groundLayer.SetCell(currentTileData.groundTile.position, 1, new(2, 0));

			fallingSand = fallingSandScene.Instantiate<Node2D>();
			GetParent().AddChild(fallingSand);
			fallingSand.Position = targetPosition - ((Vector2)targetTileDifferenceVector).Normalized() * movementDistance;
			fallingSand.GetNode<AnimationPlayer>("AnimationPlayer").Play("Fall");
		}

		// since this is the start of the move merge the event with the end of the move
		mergeNextMove = true;

		// if the previous move was forced (ice, conveyor, etc...) then merge the previous moves data with the current one
		if (previousMove != null)
		{
			// use previous data with added direction
			PreviousMove currentMove = new(changedTiles, previousMove.stamina, previousMove.candiesEaten, previousMove.balloonIsActive,
				movementDirection: previousMove.movementDirection + targetTileDifferenceVector,
				usedParadigmShift: previousMove.usedParadigmShift, leversToggled: previousMove.leversToggled);
			previousMoves.Push(currentMove);
		}
		else if (targetTileDifferenceVector.Length() > 0)
		{
			PreviousMove currentMove = new(changedTiles, gameManager.currentStamina, candiesEaten, balloonIsActive,
				movementDirection: targetTileDifferenceVector);
			previousMoves.Push(currentMove);
		}

		if ((currentTileData.groundTile.customType ?? "Void") == "Void" && balloonIsActive && targetTileDifferenceVector.Length() > 0)
		{
			balloonIsActive = false;
			balloonSprite.Play("pop");
		}
	}

	/** <summary>Enter the animation state</summary> */
	public void EnterAnimating()
	{

	}

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

		// identify what Cogito is currently standing on
		currentTileData = GetTileCustomType(currentTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);

		AttemptMove(Position);

		// sync all levers
		SetLevers(true);
	}

	/** <summary>Convert global position to the tile position at the specified tile map</summary> */
	public static Vector2I PositionToAtlasIndex(Vector2 position, TileMapLayer tileMap)
	{
		return tileMap.LocalToMap(tileMap.ToLocal(position));
	}

	/** <summary>Store all useful information about a tile</summary> */
	public class CustomTileData(TileData tileData, Vector2I position, TileMapLayer tileLayer)
	{
		public TileData tileData = tileData;
		public Vector2I atlasPosition = tileLayer.GetCellAtlasCoords(position);
		public int alternative = tileLayer.GetCellAlternativeTile(position);
		public string customType = (string)tileData?.GetCustomData("CustomType");
		public Vector2 direction = GetTileDirection(tileData);
		public Vector2I position = position;
	}

	/** <summary>Keep track of the ground and obstacle tiles at the same position</summary> */
	public class LayeredCustomTileData(CustomTileData groundTile, CustomTileData obstacleTile)
	{
		public CustomTileData groundTile = groundTile,
			obstacleTile = obstacleTile;
	}

	/** <summary>Only set the animation if its different to possibly avoid resetting the animation</summary> */
	public void SetSpriteAnimation(string animationName, float speed = 1)
	{
		// switch to the water variant of the animation if applicable
		if (gameManager.currentStamina != gameManager.maxStamina)
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
		else if (balloonIsActive && (currentTileData.groundTile.customType ?? "Void") == "Void")
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

	/** <summary>Get the direction the tile is facing (from alternate tiles)</summary> */
	public static Vector2 GetTileDirection(TileData tileData)
	{
		if (tileData == null)
			return Vector2.Right; ;

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

	/** <summary>Get the tile types at the specified position</summary> */
	public static LayeredCustomTileData GetTileCustomType(Vector2I tilePos, TileMapLayer groundLayer, TileMapLayer obstacleLayer)
	{
		CustomTileData groundData = new(groundLayer.GetCellTileData(tilePos), tilePos, groundLayer);

		CustomTileData obstacleData = new(obstacleLayer.GetCellTileData(tilePos), tilePos, obstacleLayer);

		return new(groundData, obstacleData);
	}

	/** <summary>Return true if successfully moved</summary> */
	private bool AttemptMove(Vector2 newPosition, bool teleport = false, bool dryRun = false)
	{
		// where the tile is that Cogito will move to
		Vector2I newTilePosition = PositionToAtlasIndex(
			GetParent<Node2D>().ToGlobal(newPosition),
			gameManager.obstacleLayer
		);

		// the type of tile Cogito will move to
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

		// don't move if Cogito will move to a blocking obstacle
		if (!blockingObstacles.Contains(newTileData.obstacleTile.customType)
			&& !((currentTileData.groundTile.customType == "Conveyor" || currentTileData.groundTile.customType == "EvilConveyor") 
			&& movementDirection == -1 * currentTileData.groundTile.direction)
			&& newTilePosition.X >= 0 && newTilePosition.Y >= 0 && newTilePosition.X < screenTileDimensions.X && newTilePosition.Y < screenTileDimensions.Y)
		{
			// reset buffered input value
			bufferedInput = Vector2.Zero;

			// simply return true if a dry run to skip actually moving Cogito
			if (dryRun)
				return true;

			teleported = teleport;
			targetPosition = newPosition;
			targetTileDifferenceVector = newTilePosition - currentTileData.groundTile.position;

			if (!teleported)
				SetCogitoState(CogitoState.moving);

			return true;
		}

		mergeNextMove = false;
		return false;
	}

	/** <summary>Toggle FlipH to make the sprite look the opposite way, called from animation player</summary> */
	public void ToggleSpriteFlipH()
	{
		animatedSprite.FlipH = !animatedSprite.FlipH;
	}

	/** <summary>Open the lose menu, called from animation player</summary> */
	public void Lose()
	{
		Engine.TimeScale = 0;
		loseMenu.Visible = true;
		loseMenu.GetNode<Button>("VBoxContainer/UndoButton").GrabFocus();
	}

	/** <summary>Get the direction of the input, not allowing for diagonal inputs</summary> */
	public static Vector2 GetInputDirection()
	{
		if (Input.IsActionPressed("Left") ^ Input.IsActionPressed("Right")
			^ Input.IsActionPressed("Up") ^ Input.IsActionPressed("Down"))
		{
			// read the inputs of the player
			Vector2 inputDirection = Input.GetVector("Left", "Right", "Up", "Down");

			return inputDirection;
		}

		return Vector2.Zero;
	}

	/** <summary>Called at the end of the paradigm shift animation</summary> */
	public void EndParadigmShiftAnimation()
	{
		AttemptMove(Position);
		SetCogitoState(CogitoState.idle);
	}

	/** <summary>Update the tile data Cogito is on</summary> */
	public void UpdateCurrentTileData()
	{
		// convert position to tile positions
		Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, gameManager.obstacleLayer);

		// identify what Cogito is currently standing on
		currentTileData = GetTileCustomType(currentTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);
	}

	/** <summary>Run every frame to update movement and check what happens when Cogito stops</summary> */
	public bool UpdateMove()
	{
		// move Cogito to target position
		if (currentCogitoState == CogitoState.moving)
		{
			if (Input.IsActionJustPressed("Left") ^ Input.IsActionJustPressed("Right")
				^ Input.IsActionJustPressed("Up") ^ Input.IsActionJustPressed("Down"))
			{
				bufferedInput = GetInputDirection();
			}

			Velocity = ((Vector2)targetTileDifferenceVector).Normalized() * movementSpeed;

			MoveAndSlide();

			// skip checking movement inputs if Cogito is too far from target destination
			if ((Position - targetPosition).Length() >= 2)
			{
				return false;
			}
			// stop moving when close enough to target position
			else
			{
				SetCogitoState(CogitoState.idle);
			}
		}

		return true;
	}

	/** <summary>Runs every physics frame</summary> */
	public override void _PhysicsProcess(double delta)
	{
		if (gameManager.IsLevelSelect())
			return;

		// toggle pause menu only if no other menu is visible
		if (Input.IsActionJustPressed("Pause") && !winMenu.Visible && !loseMenu.Visible)
		{
			pauseMenu.Visible = !pauseMenu.Visible;

			pauseMenu.GetNode<Button>("VBoxContainer/ContinueButton").GrabFocus();

			Engine.TimeScale = Math.Abs(Engine.TimeScale - 1);
		}

		// allow undoing on lose menu
		if (Input.IsActionJustPressed("Undo") && Engine.TimeScale == 0 && loseMenu.Visible)
		{
			loseMenu.Visible = false;
			Engine.TimeScale = 1;
			mergeNextMove = false;
			Undo();
		}

		// don't allow controlling character while game is paused
		if (Engine.TimeScale == 0)
			return;

		// reset the level when reset button is pressed
		if (Input.IsActionJustPressed("Reset"))
		{
			winMenu.OnRestartPressed();
		}
		else if (Input.IsActionJustPressed("Undo"))
		{
			mergeNextMove = false;
			Undo();
		}

		// don't allow controlling character while dying but allow resetting
		if (currentCogitoState == CogitoState.animating)
		{
			if (Input.IsActionJustPressed("Undo"))
				Undo();

			return;
		}


		if (!UpdateMove() || currentCogitoState != CogitoState.idle)
			return;

		Vector2 inputDirection = GetInputDirection();

		// i'm sammyrog
		if (inputDirection != Vector2.Zero && (holdToMove || (!holdToMove && (Input.IsActionJustPressed("Left")
			|| Input.IsActionJustPressed("Right") || Input.IsActionJustPressed("Up") || Input.IsActionJustPressed("Down")))))
		{
			// where Cogito will move
			Vector2 newPosition = Position + inputDirection * movementDistance;

			AttemptMove(newPosition);
		}
		else if (bufferedInput != Vector2.Zero && AttemptMove(Position + bufferedInput * movementDistance))
		{
			// :bother:
		}
		// don't allow paradigm shifting if none are remaining
		else if (Input.IsActionJustPressed("ParadigmShift") && gameManager.paradigmShiftsRemaining > 0)
		{
			bufferedInput = Vector2.Zero;
			teleported = false;
			mergeNextMove = false;

			SetCogitoState(CogitoState.animating);
			animationPlayer.Play("ParadigmShift", customSpeed: 1);

			// game manager updates the remaining count
			gameManager.ParadigmShifted(1);

			PreviousMove currentMove = new(new LayeredCustomTileData[20,12], gameManager.currentStamina, candiesEaten, balloonIsActive,
				usedParadigmShift: true);
			previousMoves.Push(currentMove);
		}
	}

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
			// teleport if its not the teleporter Cogito is currently on and Cogito didn't just come from a teleporter
			if (teleporterPosition != currentTileData.groundTile.position && !(teleporterAtlasPosition == previousTileAtlasPosition && teleported))
			{
				Vector2 teleporterPositionDifference = (Vector2)(teleporterPosition - currentTileData.groundTile.position) * movementDistance;

				if (dryRun)
					return AttemptMove(Position + teleporterPositionDifference, true, true);

				mergeNextMove = AttemptMove(Position + teleporterPositionDifference, true);

				// only actually move and return true if successfully teleported
				if (mergeNextMove)
				{
					// instantly tp to destination if teleporting
					Position = targetPosition;
					UpdateCurrentTileData();

					if (previousMoves.Count > 0)
					{
						PreviousMove previousMove = previousMoves.Pop();

						// use previous data with added direction
						PreviousMove currentMove = new(previousMove.changedTiles, gameManager.currentStamina, candiesEaten, previousMove.balloonIsActive,
							movementDirection: previousMove.movementDirection + targetTileDifferenceVector, 
							usedParadigmShift: previousMove.usedParadigmShift, leversToggled: previousMove.leversToggled);
						previousMoves.Push(currentMove);
					}

					targetTileDifferenceVector = new(0, 0);

					return true;
				}
			}
		}

		return false;
	}

	/** <summary>Activate the paradigm shift ability which shifts the correct crystals and levers</summary> */
	public void ParadigmShift()
	{
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

		// keep track of crystals shifted if it needs to be undone
		List<Vector2I> shiftedCogCrystals = [],
			shiftedReinforcedCogCrystals = [],
			shiftedDeinforcedCogCrystals = [],
			shiftedLevers = [],
			demolishedRocks = [];

		// replace both CogCrystals and ReinforcedCogCrystals with a cog for adjacent tiles
		foreach (Vector2I adjacentCoordinate in adjacentCoordinates)
		{
			CustomTileData obstacleData = new(gameManager.obstacleLayer.GetCellTileData(adjacentCoordinate), adjacentCoordinate, 
				gameManager.obstacleLayer);

			switch (obstacleData.customType)
			{
				// add each object shifted to the appropriate list
				case "ReinforcedCogCrystal":
					shiftedReinforcedCogCrystals.Add(adjacentCoordinate);
					break;
				case "CogCrystal":
					shiftedCogCrystals.Add(adjacentCoordinate);
					break;
				case "LeverLeft":
				case "LeverRight":
					shiftedLevers.Add(adjacentCoordinate);
					break;
				case "Rock":
					demolishedRocks.Add(adjacentCoordinate);
					break;
			}
		}

		// replace only normal CogCrystals with a cog for diagonal tiles
		foreach (Vector2I diagonalCoordinate in diagonalCoordinates)
		{
			CustomTileData obstacleData = new(gameManager.obstacleLayer.GetCellTileData(diagonalCoordinate), diagonalCoordinate, 
				gameManager.obstacleLayer);

			switch (obstacleData.customType)
			{
				// add each object shifted to the appropriate list
				case "CogCrystal":
					shiftedCogCrystals.Add(diagonalCoordinate);
					break;
				case "DeinforcedCogCrystal":
					shiftedDeinforcedCogCrystals.Add(diagonalCoordinate);
					break;
				case "LeverLeft":
				case "LeverRight":
					shiftedLevers.Add(diagonalCoordinate);
					break;
			}
		}

		// create a list that contains normal and reinforced cog crystals
		List<Vector2I> totalShiftedCogCrystals = [.. shiftedCogCrystals];
		totalShiftedCogCrystals.AddRange(shiftedReinforcedCogCrystals);
		totalShiftedCogCrystals.AddRange(shiftedDeinforcedCogCrystals);

		LayeredCustomTileData[,] changedTiles = new LayeredCustomTileData[20,12];

		// convert all crystals shifted to cogs
		foreach (Vector2I crystalPosition in totalShiftedCogCrystals)
		{
			changedTiles[crystalPosition.X, crystalPosition.Y] = GetTileCustomType(crystalPosition, 
				gameManager.groundLayer, gameManager.obstacleLayer);
			gameManager.obstacleLayer.SetCell(crystalPosition, 1, new(5, 1));
		}

		// keep track if levers were toggled to save for undo information
		bool leversJustToggled = false;

		// toggle levers if at least one was shifted
		if (shiftedLevers.Count >= 1)
		{
			leversJustToggled = true;
			ToggleLevers();
		}

		// remove all adjacent rocks if candy has been eaten
		if (candiesEaten > 0 && demolishedRocks.Count > 0)
		{
			candiesEaten--;

			foreach (Vector2I rockPosition in demolishedRocks)
			{
				changedTiles[rockPosition.X, rockPosition.Y] = GetTileCustomType(rockPosition, 
					gameManager.groundLayer, gameManager.obstacleLayer);
				gameManager.obstacleLayer.SetCell(rockPosition);
			}
		}

		// previous move was just lowering paradigm shift count but this will merge with the crystals removed to allow undoing mid animation
		PreviousMove previousMove = previousMoves.Pop();

		// save paradigm shift data to be undone
		PreviousMove currentMove = new(changedTiles, gameManager.currentStamina, previousMove.candiesEaten, balloonIsActive,
			usedParadigmShift: true, leversToggled: leversJustToggled);
		previousMoves.Push(currentMove);

		// check if any un-shifted crystals remain and when out of shifts and if so, show fail menu
		if (gameManager.paradigmShiftsRemaining == 0)
		{
			var reinforcedCogCrystals = gameManager.obstacleLayer.GetUsedCellsById(1, new(4, 1));
			var deinforcedCogCrystals = gameManager.obstacleLayer.GetUsedCellsById(1, new(6, 2));
			var cogCrystals = gameManager.obstacleLayer.GetUsedCellsById(1, new(3, 1));

			if (reinforcedCogCrystals.Count + deinforcedCogCrystals.Count + cogCrystals.Count > 0)
			{
				Lose();
			}
		}
	}

	/** <summary>Set all levers to the specified direction but doesn't adjust conveyors</summary> */
	public void SetLevers(bool leversAreLeft)
	{
		this.leversAreFacingLeft = leversAreLeft;

		// get all levers
		var levers = gameManager.obstacleLayer.GetUsedCellsById(1, new(6, 1));
		levers.AddRange(gameManager.obstacleLayer.GetUsedCellsById(1, new(7, 1)));

		foreach (Vector2I leverPosition in levers)
		{
			if (!leversAreLeft)
			{
				gameManager.obstacleLayer.SetCell(leverPosition, 1, new(7, 1));
			}
			else
			{
				gameManager.obstacleLayer.SetCell(leverPosition, 1, new(6, 1));
			}
		}
	}

	/** <summary>Toggle all levers, switching all conveyors</summary> */
	public void ToggleLevers()
	{
		// set levers to the opposite direction
		SetLevers(!leversAreFacingLeft);

		// get all conveyors
		var conveyors = gameManager.groundLayer.GetUsedCellsById(1, new(0, 2));

		// flip the direction of every conveyor
		foreach (Vector2I conveyorPosition in conveyors)
		{
			CustomTileData conveyorData = new(gameManager.groundLayer.GetCellTileData(conveyorPosition), conveyorPosition,
				gameManager.groundLayer);
			int alt = 0;

			if (conveyorData.direction.X == 1) alt = 1;
			else if (conveyorData.direction.X == -1) alt = 0;
			else if (conveyorData.direction.Y == 1) alt = 3;
			else if (conveyorData.direction.Y == -1) alt = 2;

			gameManager.groundLayer.SetCell(
				conveyorPosition,
				1,
				new Vector2I(0, 2),
				alt
			);
		}
	}

	/** <summary>Undo the last saved move</summary> */
	public void Undo()
	{
		if (previousMoves.Count < 1)
			return;

		// cancel buffered inputs
		bufferedInput = Vector2.Zero;

		mergeNextMove = false;

		// get the latest move's data
		PreviousMove previousMove = previousMoves.Pop();

		// delete any falling sand if it exists
		if (IsInstanceValid(fallingSand))
			fallingSand.QueueFree();

		// move the canine to the previous position
		if (previousMove.movementDirection != Vector2I.Zero)
		{
			Position -= (Vector2)previousMove.movementDirection * movementDistance;
		}
		

		foreach (LayeredCustomTileData tileData in previousMove.changedTiles)
		{
			if (tileData == null)
				continue;

			if (tileData.obstacleTile.customType == "Cog" || ((tileData.obstacleTile.customType == "CogCrystal" || tileData.obstacleTile.customType == "ReinforcedCogCrystal"
				|| tileData.obstacleTile.customType == "DeinforcedCogCrystal") && gameManager.obstacleLayer.GetCellSourceId(tileData.groundTile.position) == -1))
			{
				// turn the goal back off if it was on		
				if (gameManager.cogsChallenged == gameManager.TotalNumberOfCogs)
				{
					gameManager.groundLayer.SetCell(gameManager.goalCoordinates, 1, new(1, 1));
				}

				// adjust counter
				gameManager.CogChallenged(-1);	
			}

			gameManager.groundLayer.SetCell(tileData.groundTile.position, 1, 
					tileData.groundTile.atlasPosition, tileData.groundTile.alternative);
			
			gameManager.obstacleLayer.SetCell(tileData.obstacleTile.position, 1, 
					tileData.obstacleTile.atlasPosition, tileData.obstacleTile.alternative);
		}

		candiesEaten = previousMove.candiesEaten;
		balloonIsActive = previousMove.balloonIsActive;

		if (balloonIsActive)
		{
			balloonSprite.Visible = true;
			balloonSprite.Play("default");
		}
		else
		{
			balloonSprite.Visible = false;
		}

		// un-shift crystals 
		if (previousMove.usedParadigmShift)
		{
			// adjust counter
			gameManager.ParadigmShifted(-1);
		}

		if (previousMove.leversToggled)
			ToggleLevers();

		gameManager.StaminaChanged(gameManager.currentStamina - previousMove.stamina, this);

		// update the tile data Cogito is currently on after restoring everything
		UpdateCurrentTileData();

		targetTileDifferenceVector = (Vector2I)previousMove.movementDirection;

		SetCogitoState(CogitoState.idle);
	}
}
