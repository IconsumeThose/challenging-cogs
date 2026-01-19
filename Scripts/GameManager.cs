using Godot;
using System;

public partial class GameManager : Node2D
{
	public static int currentWorld = 1;
	public static int currentLevel = 1;
	
	[Export]
	public int maxParadigmShifts = 1;

	// do not use this variable, use TotalNumberOfCogs
	private int totalNumberOfCogs = -1;

	[Export] public Ui ui;

	// don't allow setting the variable and calculate the correct value exactly once
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
				var inforcedCogCrystals = obstacleLayer.GetUsedCellsById(1, new(3, 1));
				totalNumberOfCogs = cogs.Count + reinforcedCogCrystals.Count + inforcedCogCrystals.Count;	
			}

			return totalNumberOfCogs;
		}
	}

	public int paradigmShiftsRemaining = 0,
		cogsChallenged = 0;

	[Export]
	public TileMapLayer obstacleLayer,
		groundLayer;

	private Vector2I goalCoordinates;

	public void CalculateCurrentWorldAndLevel()
	{
		string scenePath = GetTree().CurrentScene.SceneFilePath;

		int worldNumberDigits = scenePath[26] == '/' ? 1 : 2;
		
		// get the current world for testing when launching scene directly from godot editor (f6)
		currentWorld = scenePath.Substring(25, worldNumberDigits).ToInt();
		
		int levelNumberDigits = scenePath[32 + worldNumberDigits] == '.' ? 1 : 2;

		// get the current level for testing when launching scene directly from godot editor (f6)
		currentLevel = scenePath.Substring(31 + worldNumberDigits, levelNumberDigits).ToInt();
		
		// set shifts remaining to the max that was set
		paradigmShiftsRemaining = maxParadigmShifts;
	}

	// initialize the game manager
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
	}

	// update the paradigm shift counts and ui
	public void ParadigmShifted()
	{
		paradigmShiftsRemaining--;

		ui.UpdateParadigmShiftCountLabel(paradigmShiftsRemaining);
	}

	// update the cogs challenged count and ui
	public void CogChallenged()
	{
		cogsChallenged++;
		ui.UpdateCogCountLabel(cogsChallenged);

		// if all cogs were challenged, turn the goal on
		if (cogsChallenged == totalNumberOfCogs)
		{
			groundLayer.SetCell(goalCoordinates, 1, new(2, 1));
		}
	}
}
