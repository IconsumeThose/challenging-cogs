using Godot;
using System.Collections.Generic;
using System;
using static GameManager;

public partial class Cogito : Character
{	
	/** <summary>Time the reset button must be held to reset the game if setting is enabled</summary> */
	[Export] public double resetHoldTime = 0.5;

	/** <summary>Original sand node used to handle the falling animation. These will be instantiated and the sprite corrects to the world automatically</summary> */
	[Export] public AnimatedSprite2D fallingSandSprite;

	/** <summary>Used for the falling sand animation</summary> */
	public PackedScene fallingSandScene = new();

	[Export]
	/** <summary>Reference to win menu in scene</summary> */
	public Menu winMenu,

		/** <summary>Reference to pause menu in scene</summary> */
		pauseMenu,

		/** <summary>Reference to lose menu in scene</summary> */
		loseMenu;

	/** <summary>Reference to Cogito's main animated sprite 2D</summary> */
	[Export]
	public AnimatedSprite2D balloonSprite;

	/** <summary>True when levers are facing left</summary> */
	public bool leversAreFacingLeft = true;

	/** <summary>True when character has balloon, used for BalloonIsActive property only</summary> */
	private bool balloonIsActive = false;
	
	/** <summary>The amount of time the reset button has been held down in seconds</summary> */
	private double resetHeldTime = 0;

	/** <summary>Store the input direction to be buffered</summary> */
	private Vector2 bufferedInput = Vector2.Zero;

	/** <summary>The amount of candies eaten</summary> */
	private int candiesEaten = 0;

	/** <summary>True right after an undo occurred; Checked to prevent unwanted behavior</summary> */
	public bool undoHappened = false;

	/** <summary>Ran during start up</summary> */
	public override void _Ready()
	{
		base._Ready();
		
		gameManager.currentStamina = gameManager.maxStamina;

		// sync all levers
		SetLevers(true);

		SpriteFrames fallingSandFrames = fallingSandSprite.SpriteFrames,
			balloonFrames = balloonSprite.SpriteFrames;

		fallingSandFrames.Clear("default");

		// get the current sprite sheet source for the level
		TileSetSource source = gameManager.groundLayer.TileSet.GetSource(1);

		// instantiate new atlas textures to create world specific animations
		AtlasTexture fallingSandTexture = new(),
			balloonTexture = new(),
			balloonPoppedTexture = new(),
			emptyTexture = new();

		if (source is TileSetAtlasSource atlasSource)
		{
			// get full atlas texture
			Texture2D fullTexture = atlasSource.Texture;

			// set atlas texture for all animation frames to sprite sheet
			fallingSandTexture.Atlas = balloonTexture.Atlas = balloonPoppedTexture.Atlas = emptyTexture.Atlas = fullTexture;

			// set texture regions to where the associated tiles are
			fallingSandTexture.Region = new(new(0, 0), tileSize * new Vector2(1, 1));
			balloonTexture.Region = new(tileSize * new Vector2(6, 4), tileSize * new Vector2(1, 2));
			balloonPoppedTexture.Region = new(tileSize * new Vector2(7, 4), tileSize * new Vector2(1, 2));
			emptyTexture.Region = new(tileSize * new Vector2(8, 8), new(1, 1));
		}

		// add all the frames to the associated animations
		fallingSandFrames.AddFrame("default", fallingSandTexture);

		balloonFrames.Clear("default");
		balloonFrames.AddFrame("default", balloonTexture);

		balloonFrames.Clear("pop");
		balloonFrames.AddFrame("pop", balloonTexture);
		balloonFrames.AddFrame("pop", balloonPoppedTexture);
		balloonFrames.AddFrame("pop", emptyTexture);

		// build and pack the falling sand sprite so it can be instantiated
		fallingSandSprite.Visible = true;
		fallingSandScene.Pack(fallingSandSprite);

		// delete the current instance of falling sand as its no longer needed
		fallingSandSprite.QueueFree();
	}

	/** <summary>Runs every physics frame</summary> */
	public override void _PhysicsProcess(double delta)
	{
		// enable hitbox at start of frame
		Area2D hitbox = GetNode<Area2D>("Area2D");
		hitbox.Monitoring = true;
		
		base._PhysicsProcess(delta);
	}
	
	protected override void ExitIdle()
	{
		base.ExitIdle();

		// tell the game manager that cogito's move ended
		if (!undoHappened && targetCharacterState != CharacterState.animating)
			gameManager.CogitoMoved();
	}

	/** <summary> Specifically handle colliding with a snake to die</summary> */
	protected override void OnCharacterCollision(Node2D body)
	{
		// ensure the collision was a snake that is alive
		if (body is not Snake snake || snake.currentCharacterState == CharacterState.dead || undoHappened)
			return;

		SetCharacterState(CharacterState.animating);
		
		stoppedMidMovement = true;
	
		// TODO: see if this code block actually ever comes to play
		if (currentCharacterState == CharacterState.moving)
		{
			PreviousMove previousMove = gameManager.previousMoves.Pop();
			previousMove.movementDirections[this].directionMoved = Vector2I.Zero;
			UpdatePreviousMove(previousMove, previousMove.changedTiles, false);
		}

		StartDeath("Fall", .5f);

		// stop the snake that was collided with too
		snake.stoppedMidMovement = true;
		snake.SetCharacterState(CharacterState.idle);
	}
	
	/** <summary>Start a new movement log for undoing with support for logging Cogito-specific items</summary> */
	protected override void SaveNewMove(LayeredCustomTileData[,] changedTiles)
	{
		gameManager.savedMove = gameManager.currentMove;
	
		PreviousMove currentMove = new(gameManager.currentMove, changedTiles, gameManager.currentStamina, candiesEaten,
			balloonIsActive, movementDirections: new() { 
				{ this, new(targetTileDifferenceVector) } 
			}
		);

		gameManager.previousMoves.Push(currentMove);
	}

	/** <summary>Reset stamina when Cogito is out of water</summary> */
	protected override void OutOfWaterInteraction()
	{
		base.OutOfWaterInteraction();
		gameManager.StaminaChanged(-99999999, this);
	}

	/** <summary>Subtract one stamina when moving through water</summary> */
	protected override void WaterInteraction()
	{
		base.WaterInteraction();
		gameManager.StaminaChanged(1, this);
	}

	/** <summary>Collect the cog!</summary> */
	protected override void CogInteraction()
	{
		base.CogInteraction();
		gameManager.CogChallenged(1);
		gameManager.obstacleLayer.SetCell(currentTileData.groundTile.position);
	}

	/** <summary>Eat the candy!</summary> */
	protected override void CandyInteraction()
	{
		base.CandyInteraction();
		candiesEaten++;
		gameManager.obstacleLayer.SetCell(currentTileData.groundTile.position);
	}

	/** <summary>Equip the balloon!</summary> */
	protected override void BalloonInteraction()
	{
		base.BalloonInteraction();

		// don't do anything if Cogito already has a balloon
		if (!balloonIsActive)
		{
			balloonIsActive = true;
			balloonSprite.Visible = true;
			balloonSprite.Animation = "default";
			gameManager.obstacleLayer.SetCell(currentTileData.groundTile.position);
			SetSpriteAnimation("Float");
		}
	}

	protected override bool BalloonIsActive { get { return balloonIsActive; } }

	protected override void PopBalloon()
	{
		base.PopBalloon();

		balloonIsActive = false;
		balloonSprite.Play("pop");
	}

	/** <summary>Pop the active balloon when teleporting</summary> */
	protected override void TelePop()
	{
		base.TelePop();

		if (balloonIsActive)
		{
			PopBalloon();
		}
	}

	/** <summary>Win when on an active goal</summary> */
	protected override void GoalInteraction()
	{
		base.GoalInteraction();
		
		Engine.TimeScale = 0;
		winMenu.Visible = true;
		winMenu.GetNode<Button>("VBoxContainer/NextLevelButton").GrabFocus();

		DataManager.SaveGame();
	}

	/** <summary>For Cogito, sand falls when they walk off of that tile</summary> */
	protected override void SandInteraction(LayeredCustomTileData[,] changedTiles)
	{
		base.SandInteraction(changedTiles);
		
		changedTiles[currentTileData.groundTile.position.X, currentTileData.groundTile.position.Y] = currentTileData;

		// make sand fall after walking off that tile
		gameManager.groundLayer.SetCell(currentTileData.groundTile.position, 1, new(2, 0));

		fallingSand = fallingSandScene.Instantiate<Node2D>();
		GetParent().AddChild(fallingSand);
		fallingSand.Position = TargetPosition - ((Vector2)targetTileDifferenceVector).Normalized() * movementDistance;
		fallingSand.GetNode<AnimationPlayer>("AnimationPlayer").Play("Fall");
	}

	protected override void MoveInit(Vector2 newPosition, bool teleport, bool dryRun, Vector2I newTilePosition)
	{
		// reset buffered input value
		bufferedInput = Vector2.Zero;

		base.MoveInit(newPosition, teleport, dryRun, newTilePosition);
	}

	public override void Lose()
	{
		base.Lose();
		
		Engine.TimeScale = 0;
		loseMenu.Visible = true;
		loseMenu.GetNode<Button>("VBoxContainer/UndoButton").GrabFocus();
	}

	/** <summary>Get the direction of the input, not allowing for diagonal inputs</summary> */
	public override Vector2 GetInputDirection()
	{
		Vector2 inputDirection = Input.GetVector("Left", "Right", "Up", "Down");
		
		if (inputDirection.X == inputDirection.Y)
		{
			// set input to zero if going diagonal
			inputDirection = Vector2.Zero;
		}
		else
		{
			// choose the strongest axis for direction for joystick
			inputDirection = Math.Abs(inputDirection.X) > Math.Abs(inputDirection.Y) ? new(inputDirection.X, 0) : new(0, inputDirection.Y);
			inputDirection = inputDirection.Normalized(); 
		}

		if (inputDirection.Length() > 0)
		{
			return inputDirection;
		}

		return Vector2.Zero;
	}

	protected override void SetBufferedInput()
	{
		if (Input.IsActionJustPressed("Left") || Input.IsActionJustPressed("Right")
			|| Input.IsActionJustPressed("Up") || Input.IsActionJustPressed("Down"))
		{
			bufferedInput = GetInputDirection();
		}
	}

	protected override void ProcessBeforePauseCheck(double delta)
	{
		// toggle pause menu only if no other menu is visible
		if (Input.IsActionJustPressed("Pause") && !winMenu.Visible && !loseMenu.Visible)
		{
			pauseMenu.Visible = !pauseMenu.Visible;

			pauseMenu.GetNode<Button>("VBoxContainer/ContinueButton").GrabFocus();

			Engine.TimeScale = Math.Abs(Engine.TimeScale - 1);
		}

		// allow undoing and resetting while game is running or on lose menu
		if (Engine.TimeScale == 1 || (Engine.TimeScale == 0 && loseMenu.Visible))
		{
			if (Input.IsActionJustPressed("Undo"))
			{
				loseMenu.Visible = false;
				Engine.TimeScale = 1;
				Undo();
			}
			// reset the level when reset button is pressed, allow instantly resetting while on a menu screen
			if (Input.IsActionJustPressed("Reset") && (!DataManager.holdToReset ^ Engine.TimeScale == 0))
			{
				winMenu.OnRestartPressed();
			}
			// update reset button hold time if its pressed and hold to reset is enabled
			else if (Input.IsActionPressed("Reset") && DataManager.holdToReset)
			{
				resetHeldTime += delta;

				if (resetHeldTime >= resetHoldTime)
				{
					winMenu.OnRestartPressed();
				}
			}
			else
			{
				resetHeldTime = 0;
			}
		}
	}

	protected override bool MoveWithBuffer { get { return bufferedInput != Vector2.Zero && AttemptMove(Position + bufferedInput * movementDistance); } }

	protected override bool InputDetected(Vector2 inputDirection)
	{
		return inputDirection != Vector2.Zero && (DataManager.holdToMove || (!DataManager.holdToMove && (Input.IsActionJustPressed("Left")
			|| Input.IsActionJustPressed("Right") || Input.IsActionJustPressed("Up") || Input.IsActionJustPressed("Down")))); 
	}

	/** <summary>Paradigm shift when the input is pressed and there are enough shifts remaining</summary> */
	protected override void CheckParadigmShiftInput()
	{
		if (Input.IsActionJustPressed("ParadigmShift") && gameManager.paradigmShiftsRemaining > 0)
		{
			bufferedInput = Vector2.Zero;
			teleported = false;
			
			SetCharacterState(CharacterState.animating);
			animationPlayer.Play("ParadigmShift", customSpeed: 1);

			// game manager updates the remaining count
			gameManager.ParadigmShifted(1);

			gameManager.savedMove = gameManager.currentMove;
			
			PreviousMove currentMove = new(gameManager.currentMove, new LayeredCustomTileData[20, 12], gameManager.currentStamina, 
				candiesEaten,	balloonIsActive, usedParadigmShift: true, 
				movementDirections:  new() { 
				{ this, new(Vector2I.Zero) } }
			);
			gameManager.previousMoves.Push(currentMove);
		}
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

		// keep track if levers were toggled to save for undo information
		bool leversJustToggled = false;

		// toggle levers if at least one was shifted
		if (shiftedLevers.Count >= 1)
		{
			leversJustToggled = true;
			ToggleLevers();
		}

		// create a list that contains normal and reinforced cog crystals
		List<Vector2I> totalShiftedCogCrystals = [.. shiftedCogCrystals];
		totalShiftedCogCrystals.AddRange(shiftedReinforcedCogCrystals);
		totalShiftedCogCrystals.AddRange(shiftedDeinforcedCogCrystals);

		LayeredCustomTileData[,] changedTiles = new LayeredCustomTileData[20, 12];

		// convert all crystals shifted to cogs
		foreach (Vector2I crystalPosition in totalShiftedCogCrystals)
		{
			changedTiles[crystalPosition.X, crystalPosition.Y] = GetTileCustomType(crystalPosition,
				gameManager.groundLayer, gameManager.obstacleLayer);
			gameManager.obstacleLayer.SetCell(crystalPosition, 1, new(5, 1));
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
		PreviousMove previousMove = gameManager.previousMoves.Pop();

		// save paradigm shift data to be undone
		PreviousMove currentMove = new(gameManager.currentMove, changedTiles, gameManager.currentStamina, previousMove.candiesEaten, 
			balloonIsActive, usedParadigmShift: true, leversToggled: leversJustToggled, movementDirections: previousMove.movementDirections);
		gameManager.previousMoves.Push(currentMove);

		// check if any un-shifted crystals remain and when out of shifts but any crystals remain, show fail menu
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

	/** <summary>
	  * Set all levers to the specified direction and toggles toggle tiles but doesn't adjust conveyors
	  * Conveyors are only changed in ToggleLevers to allow this method to be called when initializing a level to ensure all tiles are correct
	  </summary> */
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

		// toggle toggle tiles
		if (leversAreFacingLeft)
		{
			// replace leftToggleTileOffs with leftToggleTileOns
			ReplaceTiles(new(1, 4), new(0, 4));

			// replace rightToggleTileOns with rightToggleTileOffs
			ReplaceTiles(new(2, 4), new(3, 4));
		}
		else
		{
			// replace leftToggleTileOns with leftToggleTileOffs
			ReplaceTiles(new(0, 4), new(1, 4));

			// replace rightToggleTileOffs with rightToggleTileOns
			ReplaceTiles(new(3, 4), new(2, 4));
		}
	}

	/** <summary>Toggle all levers, switching all conveyors and toggling toggle tiles</summary> */
	public void ToggleLevers()
	{
		// set levers to the opposite direction
		SetLevers(!leversAreFacingLeft);

		// get all conveyors
		var conveyorPositions = gameManager.groundLayer.GetUsedCellsById(1, new(0, 2));

		// flip the direction of every conveyor
		foreach (Vector2I conveyorPosition in conveyorPositions)
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
				new(0, 2),
				alt
			);
		}
	}

	/** <summary>Undo the last saved move</summary> */
	public void Undo()
	{
		if (gameManager.previousMoves.Count < 1)
			return;

		// cancel buffered inputs
		bufferedInput = Vector2.Zero;

		gameManager.currentMove--;
		gameManager.savedMove--;

		// get the latest move's data
		PreviousMove previousMove = gameManager.previousMoves.Pop();

		// delete any falling sand if it exists
		if (IsInstanceValid(fallingSand))
			fallingSand.QueueFree();

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

		undoHappened = true;
		Area2D hitbox = GetNode<Area2D>("Area2D");

		// temporarily disable hitbox monitoring as positions are reset one character at a time and there might be a moment where the characters overlap
		hitbox.Monitoring = false;
		
		foreach (KeyValuePair<Character, CharacterMovement> characterPositionPair in previousMove.movementDirections)
		{
			Character character = characterPositionPair.Key;
			CharacterMovement characterMovement = characterPositionPair.Value;

			if (character.stoppedMidMovement)
				character.Position = character.TargetPosition;

			character.stoppedMidMovement = false;

			character.Position -= (Vector2)characterMovement.directionMoved * movementDistance;

			character.targetTileDifferenceVector = characterMovement.directionMoved;

			// set the snake's movement direction to what was logged
			if (character is Snake snake)
			{
				snake.direction = characterMovement.firstDirection;
				snake.triedOtherDirection = false;
			}

			character.UpdateSpriteDirection(characterMovement.firstDirection);

			// update the tile data Cogito is currently on after restoring everything
			character.UpdateCurrentTileData();

			character.SetCharacterState(CharacterState.idle);
		}

		undoHappened = false;
	}
}