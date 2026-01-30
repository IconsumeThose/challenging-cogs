using Godot;
using System.Collections.Generic;
using System;

public partial class Cogito : CharacterBody2D
{
	/** <summary>
		Class <c>PreviousMove</c> keeps track of all relevant information for a move so that it can be undone
		</summary> */
	public class PreviousMove(Vector2? movementDirection = null, List<Vector2I> shiftedCogCrystals = null, List<Vector2I> shiftedReinforcedCogCrystals = null, 
		Vector2I? fallenSandPosition = null, List<Vector2I> challengedCogPositions = null, bool usedParadigmShift = false, bool leversToggled = false)
	{
		/** <summary> The direction that cogito moved</summary> */
		public Vector2? movementDirection = movementDirection ?? Vector2.Zero;
		public readonly List<Vector2I> shiftedCogCrystals = shiftedCogCrystals ?? [];
		public readonly List<Vector2I> shiftedReinforcedCogCrystals = shiftedReinforcedCogCrystals ?? [];
		public Vector2I? fallenSandPosition = fallenSandPosition ?? Vector2I.Up;
		
		/** <summary>A list of all cogs collected in this move. Needed as multiple cogs can be collected in one move from sliding on ice</summary> */
		public List<Vector2I> challengedCogCoordinates = challengedCogPositions ?? [];
		public bool usedParadigmShift = usedParadigmShift;
		public bool leversToggled = leversToggled;
	}

	/** <summary>stack of all previous moves so that they can be undone in correct order(LIFO)</summary> */
	public readonly Stack<PreviousMove> previousMoves = new();

	/** <summary> the state cogito is in </summary> */
	public enum CogitoState
	{
		moving,
		animating,
		idle
	}
	
	/** <summary>The distance cogito moves every time  </summary> */
	[Export] public int tileSize = 32;

	/** <summary>The screen dimensions in tiles that cogito is in</summary> */ 
	[Export] public Vector2I screenTileDimensions = new(20, 11);
	
	/** <summary>The speed in which cogito moves</summary> */
	[Export] public float movementSpeed = 150;

	/** <summary>setting to allow holding down a direction to keep moving in that direction</summary> */ 
	[Export] public bool holdToMove = true;

	[Export] GameManager gameManager;

	[Export]
	public Menu winMenu,
		pauseMenu,
		loseMenu;

	[Export] public AnimatedSprite2D animatedSprite;
	[Export] public AnimationPlayer animationPlayer;

	/** <summary>Used for the falling sand animation</summary> */
	[Export] public PackedScene fallingSandScene;

	public bool leversAreLeft = true;

	/** <summary>List of all obstacles that block movement</summary> */
	private readonly List<string> blockingObstacles =
	[
		"Rock",
		"CogCrystal",
		"ReinforcedCogCrystal",
		"LeverLeft",
		"LeverRight"
	];
	
	/** <summary>Store the input direction to be buffered</summary> */
	private Vector2 bufferedInput = Vector2.Zero;

	/** <summary>Store the current data of the tiles cogito is on</summary> */
	private LayeredCustomTileData currentTileData;

	/** <summary>true if the move was forced by a special tile, needed for keeping track of moves to undo</summary> */
	private bool mergeNextMove = false,

	/** <summary>true if just teleported</summary> */
		teleported = false;

	/** <summary>the position to move to</summary> */
	private Vector2 targetPosition = Vector2.Zero;

	/** <summary>the difference vector from the original position to target in tile units, mainly used for logging teleports to undo</summary> */
	private Vector2I targetTileDifferenceVector = Vector2I.Zero;

	/** <summary>distance cogito moves to get to the next tile</summary> */
	private float movementDistance = 0;

	/** <summary> The state that cogito is currently in</summary> */
	public CogitoState currentCogitoState = CogitoState.idle;

	public void SetCogitoState(CogitoState newState)
	{
		CogitoState oldState = currentCogitoState;

		if (oldState == newState)
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

	/** <summary></summary> */
	public void ExitIdle()
	{
		
	}

	/** <summary></summary> */
	public void ExitMoving()
	{
		// set position exactly to target
		Position = targetPosition;

		// position of sand that was fallen from last move, needed to be tracked for undo
		Vector2I? fallenSandPosition = null;

		// position of any cogs that were challenged, needed to be tracked for undo and is the down vector by default (impossible normally)
		Vector2I challengedCogPosition = Vector2I.Up;

		// set sand position if found from previous tile (current tile has yet to be updated)
		if (this.currentTileData.groundTile.customType == "Sand")
		{
			fallenSandPosition = this.currentTileData.groundTile.position;
		}
		
		UpdateCurrentTileData();

		// set cog position if found
		if (currentTileData.obstacleTile.customType == "Cog")
		{
			challengedCogPosition = currentTileData.obstacleTile.position;
		}

		// if the previous move was forced (ice, conveyor, etc...) then merge the previous moves data with the current one
		if (mergeNextMove)
		{
			// there may be no previous moves if spawned on a conveyor so don't log if so
			if (previousMoves.Count > 0)
			{
				// get the previous data
				PreviousMove previousMove = previousMoves.Pop();

				// append cog position to list if one was found
				if (challengedCogPosition != Vector2I.Up)
				{
					previousMove.challengedCogCoordinates.Add(challengedCogPosition);
				}

				// use previous data with added direction
				PreviousMove currentMove = new(movementDirection: previousMove.movementDirection + targetTileDifferenceVector,
					fallenSandPosition: previousMove.fallenSandPosition, challengedCogPositions: previousMove.challengedCogCoordinates,
					shiftedCogCrystals: previousMove.shiftedCogCrystals, shiftedReinforcedCogCrystals: previousMove.shiftedReinforcedCogCrystals,
					usedParadigmShift: previousMove.usedParadigmShift, leversToggled: previousMove.leversToggled);

				previousMoves.Push(currentMove);
			}
		}
		else if (targetTileDifferenceVector.Length() > 0)
		{
			// save move information for undo normally
			List<Vector2I> challengedCogs = [];

			if (challengedCogPosition != Vector2I.Up)
			{
				challengedCogs.Add(challengedCogPosition);
			}

			PreviousMove currentMove = new(movementDirection: targetTileDifferenceVector, fallenSandPosition: fallenSandPosition,
				challengedCogPositions: challengedCogs);
			previousMoves.Push(currentMove);
		}

		mergeNextMove = false;

		// win the game if on the goal while it is activated
		if (currentTileData.groundTile.customType == "GoalOn")
		{
			Engine.TimeScale = 0;
			winMenu.Visible = true;
		}
		// if tile is void or null then make cogito fall
		else if ((currentTileData.groundTile.customType ?? "Void") == "Void")
		{
			SetCogitoState(CogitoState.animating);
			animationPlayer.Play("Fall", customSpeed: 0.5f);
		}
		// if tile is ice, make cogito continue moving in the same direction they were moving
		else if (currentTileData.groundTile.customType == "Ice" && targetTileDifferenceVector.Length() > 0)
		{
			Vector2 newPosition = Position + ((Vector2)targetTileDifferenceVector).Normalized() * movementDistance;
			mergeNextMove = AttemptMove(newPosition);
		}
		// if tile is conveyor, make cogito move in the direction the conveyor is facing
		else if (currentTileData.groundTile.customType == "Conveyor")
		{
			Vector2 newPosition = Position + currentTileData.groundTile.direction * movementDistance;
			mergeNextMove = AttemptMove(newPosition);
		}
		// if the tile is a teleporter or it is the first move and cogito has moved and nothing is blocked on the other teleporter, move
		else if (currentTileData.groundTile.customType == "Teleporter" && !(previousMoves.Count > 0
			&& targetTileDifferenceVector == Vector2.Zero) && Teleport(true))
		{
			animationPlayer.Play("Teleport");
			SetCogitoState(CogitoState.animating);
		}

		// collect the cog if its on the same tile as cogito
		if (currentTileData.obstacleTile.customType == "Cog")
		{
			gameManager.CogChallenged(1);
			gameManager.obstacleLayer.SetCell(currentTileData.groundTile.position);
		}
	}

	/** <summary></summary> */
	public void ExitAnimating()
	{
		Engine.TimeScale = 1;
		animationPlayer.Stop();
		animationPlayer.Play("RESET");
	}

	/** <summary></summary> */
	public void EnterIdle()
	{
		// set animation to idle
		SetSpriteAnimation("Idle");
	}

	/** <summary></summary> */
	public void EnterMoving()
	{
		// where the tile is that cogito will move to
		Vector2I newTilePosition = PositionToAtlasIndex(
			GetParent<Node2D>().ToGlobal(targetPosition),
			gameManager.obstacleLayer
		);

		targetTileDifferenceVector = newTilePosition - currentTileData.groundTile.position;

		// set animation accordingly to the current tile
		if (currentTileData.groundTile.customType == "Conveyor")
		{
			SetSpriteAnimation("Idle");
		}
		else if (currentTileData.groundTile.customType == "Ice")
		{
			SetSpriteAnimation("Slide");
		}
		else
		{
			SetSpriteAnimation("Move");
		}

		if (currentTileData.groundTile.customType == "Sand" && targetTileDifferenceVector.Length() > 0)
		{
			// make sand fall after walking off that tile
			gameManager.groundLayer.SetCell(currentTileData.groundTile.position, 1, new(2, 0));

			Node2D fallingSand = fallingSandScene.Instantiate<Node2D>();
			GetParent().AddChild(fallingSand);
			fallingSand.Position = targetPosition - ((Vector2)targetTileDifferenceVector).Normalized() * movementDistance;
			fallingSand.GetNode<AnimationPlayer>("AnimationPlayer").Play("Fall");
		}

		// instantly tp to destination if teleporting
		if (teleported)
		{
			Position = targetPosition;
		}
	}

	/** <summary></summary> */
	public void EnterAnimating()
	{
		
	}

	/** <summary>ran during start up</summary> */
	public override void _Ready()
	{
		movementDistance = tileSize;

		// convert position to tile positions
		Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, gameManager.obstacleLayer);

		// identify what cogito is currently standing on
		currentTileData = GetTileCustomType(currentTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);

		AttemptMove(Position);

		// sync all levers
		SetLevers(true);
	}

	// convert global position to the tile position at the specified tile map
	public static Vector2I PositionToAtlasIndex(Vector2 position, TileMapLayer tileMap)
	{
		return tileMap.LocalToMap(tileMap.ToLocal(position));
	}

	// store all useful information about a tile
	public class CustomTileData(TileData tileData, Vector2I position)
	{
		public TileData tileData = tileData;
		public string customType = (string)tileData?.GetCustomData("CustomType");
		public Vector2 direction = GetTileDirection(tileData);
		public Vector2I position = position;
	}

	// keep track of the ground and obstacle tiles at the same position
	public class LayeredCustomTileData(CustomTileData groundTile, CustomTileData obstacleTile)
	{
		public CustomTileData groundTile = groundTile,
			obstacleTile = obstacleTile;
	}

	// only set the animation if its different to possibly avoid resetting the animation
	public void SetSpriteAnimation(string animationName, float speed = 1)
	{
		if (animatedSprite.Animation != animationName)
		{
			animatedSprite.Play(animationName, speed);
		}
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
		CustomTileData groundData = new(groundLayer.GetCellTileData(tilePos), tilePos);

		CustomTileData obstacleData = new(obstacleLayer.GetCellTileData(tilePos), tilePos);

		return new(groundData, obstacleData);
	}
	

	// return true if successfully moved
	private bool AttemptMove(Vector2 newPosition, bool teleport = false, bool dryRun = false)
	{
		// where the tile is that cogito will move to
		Vector2I newTilePosition = PositionToAtlasIndex(
			GetParent<Node2D>().ToGlobal(newPosition),
			gameManager.obstacleLayer
		);

		// the type of tile cogito will move to
		LayeredCustomTileData newTileData = GetTileCustomType(newTilePosition, gameManager.groundLayer,
			gameManager.obstacleLayer);

		Vector2 movementDirection = (newPosition - Position).Normalized();

		if (!dryRun)
		{
			teleported = false;

			// reset buffered input value
			bufferedInput = Vector2.Zero;			

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

		// don't move if cogito will move to a blocking obstacle
		if (!blockingObstacles.Contains(newTileData.obstacleTile.customType) 
			&& !(currentTileData.groundTile.customType == "Conveyor" && movementDirection == -1 * currentTileData.groundTile.direction)
			&& newTilePosition.X >= 0 && newTilePosition.Y >= 0 && newTilePosition.X < screenTileDimensions.X && newTilePosition.Y < screenTileDimensions.Y)
		{
			// simply return true if a dry run to skip actually moving cogito
			if (dryRun)
				return true;

			teleported = teleport;
			targetPosition = newPosition;
			SetCogitoState(CogitoState.moving);
			
			return true;
		}

		return false;
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

	// called at the end of the paradigm shift animation
	public void EndParadigmShiftAnimation()
	{
		AttemptMove(Position);
		SetCogitoState(CogitoState.idle);
	}

	// update the tile data cogito is on
	public void UpdateCurrentTileData()
	{
		// convert position to tile positions
		Vector2I currentTilePosition = PositionToAtlasIndex(GlobalPosition, gameManager.obstacleLayer);
				
		// identify what cogito is currently standing on
		currentTileData = GetTileCustomType(currentTilePosition, gameManager.groundLayer,
				gameManager.obstacleLayer);
	}

	// run every frame to update movement and check what happens when cogito stops
	public bool UpdateMove()
	{
		// move cogito to target position
		if (currentCogitoState == CogitoState.moving)
		{
			if (Input.IsActionJustPressed("Left") ^ Input.IsActionJustPressed("Right")
				^ Input.IsActionJustPressed("Up") ^ Input.IsActionJustPressed("Down"))
			{
				bufferedInput = GetInputDirection();
			}

			Velocity = ((Vector2)targetTileDifferenceVector).Normalized() * movementSpeed;

			MoveAndSlide();

			// skip checking movement inputs if cogito is too far from target destination
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

	// runs every physics frame
	public override void _PhysicsProcess(double delta)
	{
		// for debugging allow skipping a level with the = key
		if (Input.IsActionJustPressed("SkipLevel"))
		{
			winMenu.OnNextLevelClicked();
		}

		// for debugging allow skipping a world with the - key
		if (Input.IsActionJustPressed("SkipWorld"))
		{
			winMenu.OnNextWorldClicked();
		}

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
			winMenu.OnRestartClicked();
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
			// where cogito will move
			Vector2 newPosition = Position + inputDirection * movementDistance;

			AttemptMove(newPosition);
		}
		else if (bufferedInput != Vector2.Zero)
		{
			// where cogito will move
			Vector2 newPosition = Position + bufferedInput * movementDistance;

			AttemptMove(newPosition);
		}
		// don't allow paradigm shifting if none are remaining
		else if (Input.IsActionJustPressed("ParadigmShift") && gameManager.paradigmShiftsRemaining > 0)
		{
			teleported = false;
			mergeNextMove = false;
			SetCogitoState(CogitoState.animating);
			animationPlayer.Play("ParadigmShift", customSpeed: 1);

			// game manager updates the remaining count
			gameManager.ParadigmShifted(1);

			PreviousMove currentMove = new(shiftedCogCrystals: [], shiftedReinforcedCogCrystals: [],
				usedParadigmShift: true);
			previousMoves.Push(currentMove);
		}
		else if (Input.IsActionJustPressed("Undo"))
		{
			mergeNextMove = false;
			Undo();
		}
	}

	// returns true if successfully teleported, dry run merely checks if its possible to teleport but doesn't actually teleport
	public bool Teleport(bool dryRun = false)
	{
		// check the atlas positions to see if its the same exact teleporter type
		Vector2I teleporterAtlasPosition = gameManager.groundLayer.GetCellAtlasCoords(currentTileData.groundTile.position),
			previousTileAtlasPosition = gameManager.groundLayer.GetCellAtlasCoords(currentTileData.groundTile.position - targetTileDifferenceVector);
		
		// find all instances of this teleport
		var teleporters = gameManager.groundLayer.GetUsedCellsById(1, teleporterAtlasPosition);

		foreach (Vector2I teleporterPosition in teleporters)
		{
			// teleport if its not the teleporter cogito is currently on and cogito didn't just come from a teleporter
			if (teleporterPosition != currentTileData.groundTile.position && !(teleporterAtlasPosition == previousTileAtlasPosition && teleported))
			{
				Vector2 teleporterPositionDifference = (Vector2)(teleporterPosition - currentTileData.groundTile.position) * movementDistance;

				if (dryRun)
					return AttemptMove(Position + teleporterPositionDifference, true, true);
				
				mergeNextMove = AttemptMove(Position + teleporterPositionDifference, true);
				
				// only actually move and return true if successfully teleported
				if (mergeNextMove)
				{
					UpdateMove();
					return true;	
				}
			}
		}

		return false;
	}

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
		List<Vector2I> shiftedCogCrystals = [];
		List<Vector2I> shiftedReinforcedCogCrystals = [];
		List<Vector2I> shiftedLevers = [];

		// replace both CogCrystals and ReinforcedCogCrystals with a cog for adjacent tiles
		foreach (Vector2I adjacentCoordinate in adjacentCoordinates)
		{
			CustomTileData obstacleData = new(gameManager.obstacleLayer.GetCellTileData(adjacentCoordinate), adjacentCoordinate);

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
			}
		}

		// replace only normal CogCrystals with a cog for diagonal tiles
		foreach (Vector2I diagonalCoordinate in diagonalCoordinates)
		{
			CustomTileData obstacleData = new(gameManager.obstacleLayer.GetCellTileData(diagonalCoordinate), diagonalCoordinate);

			switch (obstacleData.customType)
			{
				// add each object shifted to the appropriate list
				case "CogCrystal":
					shiftedCogCrystals.Add(diagonalCoordinate);
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
		
		// convert all crystals shifted to cogs
		foreach (Vector2I crystalPosition in totalShiftedCogCrystals)
		{
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

		// previous move was just lowering paradigm shift count but this will merge with the crystals removed to allow undoing mid animation
		previousMoves.Pop();
		
		// save paradigm shift data to be undone
		PreviousMove currentMove = new(shiftedCogCrystals: shiftedCogCrystals, shiftedReinforcedCogCrystals: shiftedReinforcedCogCrystals,
			usedParadigmShift: true, leversToggled: leversJustToggled);
		previousMoves.Push(currentMove);

		// check if any un-shifted crystals remain and when out of shifts and if so, show fail menu
		if (gameManager.paradigmShiftsRemaining == 0)
		{
			var reinforcedCogCrystals = gameManager.obstacleLayer.GetUsedCellsById(1, new(4, 1));
			var cogCrystals = gameManager.obstacleLayer.GetUsedCellsById(1, new(3, 1));

			if (reinforcedCogCrystals.Count + cogCrystals.Count > 0)
			{
				Lose();
			}
		}
	}

	// set all levers to the specified direction but doesn't adjust conveyors
	public void SetLevers(bool leversAreLeft)
	{
		this.leversAreLeft = leversAreLeft;

		// get all levers
		var levers = gameManager.obstacleLayer.GetUsedCellsById(1, new(6, 1));
		levers.AddRange( gameManager.obstacleLayer.GetUsedCellsById(1, new(7, 1)));
		
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

	// toggle all levers, switching all conveyors
	public void ToggleLevers()
	{
		// set levers to the opposite direction
		SetLevers(!leversAreLeft);

		// get all conveyors
		var conveyors = gameManager.groundLayer.GetUsedCellsById(1, new (0, 2));

		// flip the direction of every conveyor
		foreach (Vector2I conveyorPosition in conveyors)
		{
			CustomTileData conveyorData = new(gameManager.groundLayer.GetCellTileData(conveyorPosition), conveyorPosition);
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

	// undo the last saved move
	public void Undo()
	{
		if (previousMoves.Count < 1)
			return;
		
		// cancel buffered inputs
		bufferedInput = Vector2.Zero;

		mergeNextMove = false;

		// get the latest move's data
		PreviousMove previousMove = previousMoves.Pop();

		// move the canine to the previous position
		if (previousMove.movementDirection != Vector2.Zero)
			Position -= (Vector2)previousMove.movementDirection * movementDistance;

		// replace the piece of sand that may have fallen
		if (previousMove.fallenSandPosition != Vector2I.Up)
			gameManager.groundLayer.SetCell((Vector2I)previousMove.fallenSandPosition, 1, new(0, 0));

		// replace any challenged cogs
		foreach (Vector2I challengedCogPosition in previousMove.challengedCogCoordinates)
		{
			gameManager.obstacleLayer.SetCell(challengedCogPosition, 1, new(5, 1));

			// turn the goal back off if it was on		
			if (gameManager.cogsChallenged == gameManager.TotalNumberOfCogs)
			{
				gameManager.groundLayer.SetCell(gameManager.goalCoordinates, 1, new(1, 1));
			}

			// adjust counter
			gameManager.CogChallenged(-1);
		}

		// un-shift crystals 
		if (previousMove.usedParadigmShift)
		{
			// adjust counter
			gameManager.ParadigmShifted(-1);
			
			foreach (Vector2I cogCrystalPosition in previousMove.shiftedCogCrystals)
			{
				gameManager.obstacleLayer.SetCell(cogCrystalPosition, 1, new(3, 1));
			}

			foreach (Vector2I cogReinforcedCrystalPosition in previousMove.shiftedReinforcedCogCrystals)
			{
				gameManager.obstacleLayer.SetCell(cogReinforcedCrystalPosition, 1, new(4, 1));
			}
		}

		if (previousMove.leversToggled)
			ToggleLevers();

		// update the tile data cogito is currently on after restoring everything
		UpdateCurrentTileData();

		SetCogitoState(CogitoState.idle);
	}
}
