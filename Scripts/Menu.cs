using Godot;
using System;

public partial class Menu : Control
{
	[Export]
	public Button restartButton,
		closeButton;

	// restart the level
	public void OnRestartClicked()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/level.tscn");
	}

	// take you to the next level, win menu only
	public void OnNextLevelClicked()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/level2.tscn");
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

	// take you back to the main menu
	public void OnMainMenuPressed()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
	}
}
