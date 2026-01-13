using Godot;
using System;

public partial class Menu : Control
{
	[Export]
	public Button restartButton,
		closeButton;

	public void OnRestartClicked()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/level.tscn");
	}
	
	public void OnCloseClicked()
	{
		GetTree().Quit();
	}

	public void OnContinuePressed()
	{
		Visible = false;
		Engine.TimeScale = 1;
	}

	public void OnMainMenuPressed()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
	}
}
