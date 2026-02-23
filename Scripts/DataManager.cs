using Godot;
using System;

public partial class DataManager : Node
{
	public static string saveFileName = "";
	[Export(PropertyHint.GlobalSaveFile)] protected string saveFileNameInstance = "save.sav";

	public static int currentLevel = 1,
		currentWorld = 1,
		savedLevel = 1,
		savedWorld = 1;

	public static DataManager instance;

	/** <summary>Get the path of the next level</summary> */
	public static string LevelPath(int world, int level)
	{
		if (ResourceLoader.Exists($"res://Scenes/Levels/world{world}/level{level}.tscn"))
		{
			return $"res://Scenes/Levels/world{world}/level{level}.tscn";
		}
		else
		{
			return WorldPath(world + 1);
		}
	}

	/** <summary>Get the path of the next world</summary> */
	public static string WorldPath(int world)
	{
		// check if the next level even exists
		if (ResourceLoader.Exists($"res://Scenes/Levels/world{world}/level{1}.tscn"))
		{
			return $"res://Scenes/Levels/world{world}/level{1}.tscn";
		}
		else
		{
			return null;
		}
	}


	public static Vector2I ParsePathForWorldAndNumber(string scenePath)
	{
		// return value, x is world, y is level
		Vector2I worldAndLevel = new(-1, -1);
		int worldNumberDigits = scenePath[26] == '/' ? 1 : 2;

		// get the current world for testing when launching scene directly from godot editor (f6)
		worldAndLevel.X = scenePath.Substring(25, worldNumberDigits).ToInt();

		int levelNumberDigits = scenePath[32 + worldNumberDigits] == '.' ? 1 : 2;

		// get the current level for testing when launching scene directly from godot editor (f6)
		worldAndLevel.Y = scenePath.Substring(31 + worldNumberDigits, levelNumberDigits).ToInt();

		return worldAndLevel;
	}

	/** <summary>Save data to file, currently saves level and world</summary> */
	public static void SaveGame(bool bypassCheck = false)
	{
		if (!bypassCheck && !((currentWorld == savedWorld && currentLevel == savedLevel) 
			|| (currentWorld == savedWorld + 1 && currentLevel == 1))
		)
		{
			return;	
		}

		currentLevel++;

		string nextLevelPath = LevelPath(currentWorld, currentLevel);

		if (nextLevelPath != null)
		{
			Vector2I worldAndLevel = ParsePathForWorldAndNumber(nextLevelPath);

			currentWorld = worldAndLevel.X;
			currentLevel = worldAndLevel.Y;
		}
		else
		{
			currentLevel = 1;
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

	/** <summary>Order MATTERS</summary> */
	private enum SaveTypes
	{
		currentWorld,
		currentLevel
	}

	protected static int SaveCurrentLevel()
	{
		savedLevel = currentLevel;

		return currentLevel;
	}

	protected static int SaveCurrentWorld()
	{
		savedWorld = currentWorld;

		return currentWorld;
	}

	protected static void LoadCurrentLevel(Variant currentLevelData)
	{
		int currentLevel = currentLevelData.AsInt32();

		DataManager.currentLevel = currentLevel;
		savedLevel = currentLevel;
	}
	protected static void LoadCurrentWorld(Variant currentWorldData)
	{
		int currentWorld = currentWorldData.AsInt32();

		DataManager.currentWorld = currentWorld;
		savedWorld = currentWorld;
	}

	/** <summary>Reset the save file</summary> */
	public static void ResetSave()
	{
		currentLevel = 0;
		currentWorld = 1;
		SaveGame(true);
		LoadLevel(1, 1);
	}

	/** <summary>Load data from file</summary> */
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

	/** <summary>load the specified level or the next level by default</summary> */
	public static void LoadLevel(int world = -1, int level = -1)
	{
		// set default values
		if (world == -1)
			world = currentWorld;

		if (level == -1)
			level = currentLevel + 1;

		// safety checks to avoid logical errors of going 2 levels forward and bypassing current save
		if (currentWorld == savedWorld && level > savedLevel)
		{
			currentLevel = level = savedLevel;
		}
		else if (world > savedWorld)
		{
			currentWorld = world = savedWorld;

			currentLevel = level = savedLevel;
		}

		string nextLevelPath = LevelPath(world, level);

		if (nextLevelPath != null)
		{
			Engine.TimeScale = 1;
			currentLevel = level;

			instance.GetTree().ChangeSceneToFile(nextLevelPath);
			SongMixer.PlaySong((SongMixer.Song)currentWorld);
		}
		else
		{
			LoadWorld(currentWorld + 1);
		}
	}

	public static void LoadWorld(int world)
	{
		string nextWorldPath = WorldPath(world);

		// check if the next level even exists
		if (nextWorldPath != null)
		{
			Engine.TimeScale = 1;
			Vector2I worldAndLevel = ParsePathForWorldAndNumber(nextWorldPath);
			currentWorld = worldAndLevel.X;
			currentLevel = 1;

			instance.GetTree().ChangeSceneToFile(nextWorldPath);
			SongMixer.PlaySong((SongMixer.Song)currentWorld);
		}
	}

	/** <summary>Called when the node enters the scene tree for the first time.</summary> */
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
			SaveGame(true);
			LoadLevel();
		}

		// for debugging allow skipping a world with the - key
		if (Input.IsActionJustPressed("SkipWorld"))
		{
			currentLevel = 15;
			SaveGame(true);
			LoadWorld(currentWorld);
		}

		// for debugging allow quick deleting save file
		if (Input.IsActionJustPressed("DeleteSave"))
		{
			ResetSave();
		}
	}
}
