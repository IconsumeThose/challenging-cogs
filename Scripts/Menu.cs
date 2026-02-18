using Godot;
using System;

public partial class Menu : Control
{
	public override void _Ready()
	{
		if (Name == "MainMenu")
		{
			SongMixer.PlaySong(SongMixer.Song.mainMenu);
			GetNode<Button>("VBoxContainer/PlayButton").GrabFocus();
		}
		else
		{
			SongMixer.PlaySong((SongMixer.Song)DataManager.currentWorld);
		}
	}

	/** <summary>Restart the level</summary> */
	public void OnRestartClicked()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile($"res://Scenes/Levels/world{DataManager.currentWorld}/level{DataManager.currentLevel}.tscn");
		SongMixer.PlaySong((SongMixer.Song)DataManager.currentWorld);
	}

	/** <summary>Take you to the next level, win menu only or dev key pressed (=)</summary> */
	public void OnNextLevelClicked()
	{
		DataManager.LoadNextLevel();
	}

	/** <summary>Dev key only to skip world</summary> */
	public void OnNextWorldClicked()
	{
		DataManager.LoadNextWorld();
	}

	/** <summary>Closes the game</summary> */
	public void OnCloseClicked()
	{
		GetTree().Quit();
	}

	/** <summary>Specifically for the pause menu to unpause the game</summary> */
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

	/** <summary>Take you back to the main menu</summary> */
	public void OnMainMenuPressed(bool fromWinMenu)
	{
		if (!fromWinMenu)
			DataManager.currentLevel--;
		
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
	}
}
