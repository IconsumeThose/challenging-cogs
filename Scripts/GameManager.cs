using Godot;
using System;

public partial class GameManager : Node2D
{

	[Export]
	public int maxParadigmShifts = 1,
		totalWaterMoves = 3;
	[Export] public string levelName = "Name this level yo!";
	
	/** <summary>Do not use this variable, use TotalNumberOfCogs</summary> */
	private int totalNumberOfCogs = -1;

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

	public int paradigmShiftsRemaining = 0,
		cogsChallenged = 0;

	[Export]
	public TileMapLayer obstacleLayer,
		groundLayer;

	public Vector2I goalCoordinates;

	public void CalculateCurrentWorldAndLevel()
	{
		string scenePath = GetTree().CurrentScene.SceneFilePath;


		int worldNumberDigits = scenePath[26] == '/' ? 1 : 2;

		// get the current world for testing when launching scene directly from godot editor (f6)
		DataManager.currentWorld = scenePath.Substring(25, worldNumberDigits).ToInt();

		int levelNumberDigits = scenePath[32 + worldNumberDigits] == '.' ? 1 : 2;

		// get the current level for testing when launching scene directly from godot editor (f6)
		DataManager.currentLevel = scenePath.Substring(31 + worldNumberDigits, levelNumberDigits).ToInt();

		// set shifts remaining to the max that was set
		paradigmShiftsRemaining = maxParadigmShifts;
	}

	/** <summary>Initialize the game manager</summary> */
	public override void _Ready()
	{
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
			groundLayer.SetCell(goalCoordinates, 1, new(2, 1));
		}
	}
}
