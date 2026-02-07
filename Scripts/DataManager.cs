using Godot;
using System;

public partial class DataManager : Node
{
	public static string saveFileName = "a";
	[Export(PropertyHint.GlobalSaveFile)] protected string saveFileNameInstance = "save.sav";

	public static int currentLevel = 0,
		currentWorld = 1,
		savedLevel = 0,
		savedWorld = 1;

	public static DataManager instance;

	// get the path of the next level
	public static string NextLevelPath()
	{
		if (ResourceLoader.Exists($"res://Scenes/Levels/world{DataManager.currentWorld}/level{DataManager.currentLevel + 1}.tscn"))
		{
			return $"res://Scenes/Levels/world{DataManager.currentWorld}/level{DataManager.currentLevel + 1}.tscn";
		}
		else
		{
			return NextWorldPath();
		}
	}

	// get the path of the next world
	public static string NextWorldPath()
	{
		// check if the next level even exists
		if (ResourceLoader.Exists($"res://Scenes/Levels/world{DataManager.currentWorld + 1}/level{1}.tscn"))
		{
			return $"res://Scenes/Levels/world{DataManager.currentWorld + 1}/level{1}.tscn";
		}
		else
		{
			return null;
		}
	}

	// save data to file, currently saves records, seed, death positions and number of segments
	public static void SaveGame(bool bypassCheck = false)
	{
		GD.Print("starting save " + (currentWorld == savedWorld && currentLevel == savedLevel + 1) + " " + (currentWorld == savedWorld + 1 && currentLevel == 1));
		if (!bypassCheck && !((currentWorld == savedWorld && currentLevel == savedLevel + 1) 
			|| (currentWorld == savedWorld + 1 && currentLevel == 1))
		)
		{
			return;	
		}
	
		using var saveFile = FileAccess.Open($"user://{saveFileName}", FileAccess.ModeFlags.Write);

		if (saveFile == null)
		{
			GD.Print(FileAccess.GetOpenError());
		}

		// order must match the order of the enum SaveType!
		saveFile.StoreVar(SaveCurrentWorld());
		saveFile.StoreVar(SaveCurrentLevel());

		saveFile.Close();
	}

	// order MATTERS
	private enum SaveTypes
	{
		currentWorld,
		currentLevel
	}

	protected static int SaveCurrentLevel()
	{
		DataManager.savedLevel = currentLevel;
		// GD.Print("saved level " + currentLevel);
		return currentLevel;
	}

	protected static int SaveCurrentWorld()
	{
		DataManager.savedWorld = currentWorld;
		// GD.Print("saved world " + currentWorld);
		return currentWorld;
	}

	protected static void LoadCurrentLevel(Variant currentLevelData)
	{
		int currentLevel = currentLevelData.AsInt32();

		DataManager.currentLevel = currentLevel;
		DataManager.savedLevel = currentLevel;

		// GD.Print("loaded level " + currentLevel);
	}
	protected static void LoadCurrentWorld(Variant currentWorldData)
	{
		int currentWorld = currentWorldData.AsInt32();

		DataManager.currentWorld = currentWorld;
		DataManager.savedWorld = currentWorld;

		// GD.Print("loaded world " + currentWorld);
	}

	// reset the save file
	public static void ResetSave()
	{
		currentLevel = 0;
		currentWorld = 1;
		SaveGame(true);
		LoadNextLevel();
	}

	// load data from file
	public static void LoadGame()
	{
		if (!FileAccess.FileExists($"user://{saveFileName}"))
		{
			return; // don't do anything if no save file exists
		}

		using var saveFile = FileAccess.Open($"user://{saveFileName}", FileAccess.ModeFlags.ReadWrite);

		SaveTypes currentType = 0;

		while (saveFile.GetPosition() < saveFile.GetLength())
		{
			var nextData = saveFile.GetVar(true);

			switch (currentType)
			{
				case SaveTypes.currentWorld:
					LoadCurrentWorld(nextData);
					break;
				case SaveTypes.currentLevel:
					LoadCurrentLevel(nextData);
					break;
			}

			currentType++;
		}

		saveFile.Close();
	}

	public static void LoadNextLevel()
	{
		string nextLevelPath = NextLevelPath();
		// check if the next level even exists
		if (nextLevelPath != null)
		{
			Engine.TimeScale = 1;
			DataManager.currentLevel++;

			instance.GetTree().ChangeSceneToFile(nextLevelPath);
			SongMixer.PlaySong(DataManager.currentWorld);
		}
		else
		{
			LoadNextWorld();
		}
	}

	public static void LoadNextWorld()
	{
		string nextWorldPath = DataManager.NextWorldPath();
		
		// check if the next level even exists
		if (nextWorldPath != null)
		{
			Engine.TimeScale = 1;
			DataManager.currentWorld++;
			DataManager.currentLevel = 1;

			instance.GetTree().ChangeSceneToFile(nextWorldPath);
			SongMixer.PlaySong(DataManager.currentWorld);
		}
	}


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		instance = this;
		saveFileName = saveFileNameInstance;
		LoadGame();
	}

	public override void _Process(double delta)
	{
		// for debugging allow skipping a level with the = key
		if (Input.IsActionJustPressed("SkipLevel"))
		{
			LoadNextLevel();
			currentLevel--;
			SaveGame(true);
		}

		// for debugging allow skipping a world with the - key
		if (Input.IsActionJustPressed("SkipWorld"))
		{
			LoadNextWorld();
			currentLevel--;
			SaveGame(true);
		}

		// for debugging allow quick deleting save file
		if (Input.IsActionJustPressed("DeleteSave"))
		{
			ResetSave();
		}
	}
}
