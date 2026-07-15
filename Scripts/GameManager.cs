using Godot;
using System;
using System.Collections.Generic;
#pragma warning disable CA1050
public partial class GameManager : Node2D
{
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
	public class LayeredCustomTileData(CustomTileData groundTile, CustomTileData obstacleTile, Vector2I tilePosition)
	{
		public CustomTileData groundTile = groundTile,
			obstacleTile = obstacleTile;
		public Vector2I tilePosition = tilePosition;
	}

	/** <summary>Stores what character is at each tile position</summary> */
	public Character[,] characterMatrix = new Character[20, 12];

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

	/** <summary>Stores the direction moved and the first direction of a move for a character</summary> */
	public class CharacterMovement(Vector2I directionMoved)
	{
		public Vector2I directionMoved = directionMoved;
		public readonly Vector2I firstDirection = directionMoved;
	}


	/** <summary>
		Class <c>PreviousMove</c> keeps track of all relevant information for a move so that it can be undone
		</summary> */
	public class PreviousMove(int moveNumber, LayeredCustomTileData[,] changedTiles, int stamina, int candiesEaten, bool balloonIsActive = false, 
		Dictionary<Character, CharacterMovement> movementDirections = null, bool usedParadigmShift = false, bool leversToggled = false)
	{
		public int moveNumber = moveNumber;
		public readonly LayeredCustomTileData[,] changedTiles = changedTiles ?? new LayeredCustomTileData[20, 12];
		
		/** <summary> The direction that Cogito moved</summary> */
		public Dictionary<Character, CharacterMovement> movementDirections = movementDirections ?? [];
		public bool usedParadigmShift = usedParadigmShift;
		public bool leversToggled = leversToggled;
		public int stamina = stamina;
		public int candiesEaten = candiesEaten;
		public bool balloonIsActive = balloonIsActive;
	}

	/** <summary>stack of all previous moves so that they can be undone in correct order(LIFO)</summary> */
	public readonly Stack<PreviousMove> previousMoves = new();

	/** <summary>List of all characters in the level</summary> */
	public List<Character> characters = [];

	/** <summary>The current move number</summary> */
	public int currentMove = 0,

	/** <summary>The last saved move number</summary> */
		savedMove = 0;

	/** <summary>The maximum number of paradigm shifts allowed in the level</summary> */
	[Export]
	public int maxParadigmShifts = 1,

	/** <summary>The max stamina Cogito has</summary> */
		maxStamina = 0;

	/** <summary>The custom level name displayed at the top of the screen</summary> */
	[Export] public string levelName = "Name this level yo!";
	
	/** <summary>Only used for the property TotalNumberOfCogs which does the calculations</summary> */
	private int totalNumberOfCogs = -1;

	/** <summary>Sound effect for challenging a cog (collecting it)</summary> */
	[Export] public AudioStreamPlayer challengedCogSFX,

	/** <summary>Sound effect for challenging the last cog</summary> */
		challengedLastCogSFX;

	/** <summary>Reference to the Ui node displayed over each level</summary> */
	[Export] public Ui ui;

	/** <summary>Don't allow setting the variable and calculate the correct value exactly once</summary> */
	public int TotalNumberOfCogs
	{
		get
		{
			// only sum if number of cogs isn't initialized
			if (totalNumberOfCogs == -1)
			{
				// get all cogs to get the total cog count
				var cogs = obstacleLayer.GetUsedCellsById(1, new(5, 1));
				var reinforcedCogCrystals = obstacleLayer.GetUsedCellsById(1, new(4, 1));
				var deinforcedCogCrystals = obstacleLayer.GetUsedCellsById(1, new(6 ,2));
				var cogCrystals = obstacleLayer.GetUsedCellsById(1, new(3, 1));
				totalNumberOfCogs = cogs.Count + reinforcedCogCrystals.Count + deinforcedCogCrystals.Count + cogCrystals.Count;	
			}

			return totalNumberOfCogs;
		}
	}

	/** <summary>The current amount of paradigm shifts remaining</summary> */
	public int paradigmShiftsRemaining = 0,

		/** <summary>The current number of cogs challenged (collected)</summary> */
		cogsChallenged = 0,

		/** <summary>The amount of water moves Cogito current has left</summary> */
		currentStamina;

	[Export]
	/** <summary>The layer that contains all the obstacle tiles</summary> */
	public TileMapLayer obstacleLayer,

	/** <summary>The layer that contains all the ground tiles</summary> */
		groundLayer;

	/** <summary>Stores the coordinates of the goal</summary> */
	public Vector2I goalCoordinates;

	/** <summary>Reference to the Cogito in the level</summary> */
	public Cogito cogito;

	/** <summary>Checks if the level is loaded in the level select</summary> */
	public bool IsLevelSelect
	{
		get 
		{
			isLevelSelect = GetTree().CurrentScene.Name == "LevelSelect";
			return isLevelSelect;
		}
	}

	/** <summary>Identify which world and level is loaded</summary> */
	public void CalculateCurrentWorldAndLevel()
	{
		// don't do anything if in level select
		if (IsLevelSelect)
			return;

		string scenePath = GetTree().CurrentScene.SceneFilePath;
		(int world, int level) = DataManager.ParsePathForWorldAndNumber(scenePath);
		DataManager.currentWorld = world;
		DataManager.currentLevel = level;

		// set shifts remaining to the max that was set
		paradigmShiftsRemaining = maxParadigmShifts;
	}

	/** <summary>Flag for if the scene is instantiated in level select or not</summary> */
	private static bool isLevelSelect = false;

	/** <summary>returns true if all characters are idle</summary> */
	public bool AllCharactersIdle 
	{
		get
		{
			foreach (Character character in characters)
			{
				// character is considered idle if it is idle, dead, or for snakes, has the queueMove flag to indicate it will attempt to move 
				if (!(character?.currentCharacterState == character.idleState || character?.currentCharacterState == character.deadState)
					|| character is Snake snake && snake.queueMove)
				{
					return false;
				}
			}
			return true;
		}
	}

	/** <summary>Increment current move once all characters are idle or dead</summary> */
	public void CheckToIncrementCurrentMove()
	{
		if (AllCharactersIdle)
		{
			// once the character enters the idle state then the turn is completely done
			currentMove++;
		}
	}

	/** <summary>When cogito moves, trigger snakes to move</summary> */
	public void CogitoMoved()
	{
		foreach (Character character in characters)
		{
			if (character is Snake snake && snake.currentCharacterState != character.deadState)
			{
				snake.queueMove = true;
			}
		}
	}

	/** <summary>Initialize the game manager</summary> */
	public override void _Ready()
	{
		if (IsLevelSelect)
		{
			// disable the visibility of the BACKGROUND which is called TextureRect because sammy never renamed it when first setting it up
			GetParent().GetNode<TextureRect>("TextureRect").Visible = false;
			return;
		}
		
		foreach (Node child in GetParent().FindChild("ScalingParent").GetChildren())
		{
			if (child is Character character)
			{
				characters.Add(character);

				if (character is Cogito cogito)
					this.cogito = cogito;
			}
		}

		CalculateCurrentWorldAndLevel();

		// find all goals
		var offGoals = groundLayer.GetUsedCellsById(1, new(1, 1));
		var onGoals = groundLayer.GetUsedCellsById(1, new(2, 1));

		if (offGoals.Count + onGoals.Count > 1)
		{
			// throw an error if more than one goal was found
			GD.PushError("More than one goal found!");
		}
		else if (offGoals.Count + onGoals.Count == 0)
		{
			// throw an error if no goals were found
			GD.PushError("No goals found!");
		}
		else
		{
			// save the coordinate of the goal
			if (onGoals.Count == 1)
			{
				goalCoordinates = onGoals[0];

				// turn off the goal that was on if there are any cogs
				if (TotalNumberOfCogs > 0)
				{
					groundLayer.SetCell(goalCoordinates, 1, new(1, 1));
				}
			}
			else if (offGoals.Count == 1)
			{
				goalCoordinates = offGoals[0];

				// turn on the goal that was off if there are no cogs
				if (TotalNumberOfCogs == 0)
				{
					groundLayer.SetCell(goalCoordinates, 1, new(2, 1));
				}
			}
		}

		for (int i = 0; i < 4; i++)
		{
			var teleporters = groundLayer.GetUsedCellsById(1, new(4 + i, 0));

			if (teleporters.Count == 1 || teleporters.Count > 2)
			{
				GD.PushError("For each teleporter type, please put exactly 2 tiles or none!");
			}
		}
	}

	/** <summary>Update the paradigm shift counts and ui</summary> */
	public void ParadigmShifted(int count)
	{
		paradigmShiftsRemaining -= count;

		ui.UpdateParadigmShiftCountLabel(paradigmShiftsRemaining);
	}

	/** <summary>Update the cogs challenged count and ui</summary> */
	public void CogChallenged(int count)
	{
		cogsChallenged += count;
		ui.UpdateCogCountLabel(cogsChallenged);

		// if all cogs were challenged, turn the goal on
		if (cogsChallenged == totalNumberOfCogs)
		{
			challengedLastCogSFX.Play();
			groundLayer.SetCell(goalCoordinates, 1, new(2, 1));
		}
		else if (count > 0)
		{
			challengedCogSFX.Play();
		}
	}

	/** <summary>Update the stamina count and ui</summary> */
	public void StaminaChanged(int change, Character character)
	{
		currentStamina -= change;

		currentStamina = Math.Clamp(currentStamina, 0, maxStamina);

		// character drowns when reaching 0 stamina and has a specified max stamina
		if (currentStamina == 0 && maxStamina > 0 )
		{
			character.StartDeath("Drown");
		}

		ui.UpdateStaminaBar(currentStamina);
	}
}
