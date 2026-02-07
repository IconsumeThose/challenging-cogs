using Godot;
using System;

public partial class Menu : Control
{
	public override void _Ready()
	{
		if (Name == "MainMenu")
		{
			SongMixer.PlaySong(SongMixer.Song.mainMenu);
		}
		else
		{
			GD.Print(DataManager.currentWorld);
			SongMixer.PlaySong((SongMixer.Song)DataManager.currentWorld);
		}
	}

	// restart the level
	public void OnRestartClicked()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile($"res://Scenes/Levels/world{DataManager.currentWorld}/level{DataManager.currentLevel}.tscn");
		SongMixer.PlaySong((SongMixer.Song)DataManager.currentWorld);
	}

	// take you to the next level, win menu only or dev key pressed (=)
	public void OnNextLevelClicked()
	{
		DataManager.LoadNextLevel();
	}

	// dev key only to skip world
	public void OnNextWorldClicked()
	{
		DataManager.LoadNextWorld();
	}

	// closes the game
	public void OnCloseClicked()
	{
		GetTree().Quit();
	}

	// specifically for the pause menu to unpause the game
	public void OnContinuePressed()
	{
		Visible = false;
		Engine.TimeScale = 1;
	}

	public void OnUndoClicked()
	{
		Engine.TimeScale = 1;
		Visible = false;
		Cogito cogito = GetParent().FindChild("ScalingParent").FindChild("Cogito") as Cogito;
		cogito.Undo();
	}

	// take you back to the main menu
	public void OnMainMenuPressed()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
	}
}
