using Godot;
using System;

public partial class Menu : Control
{
	// restart the level
	public void OnRestartClicked()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile($"res://Scenes/Levels/world{GameManager.currentWorld}/level{GameManager.currentLevel}.tscn");
		Songmixer.PlaySong(GameManager.currentWorld);
	}

	// take you to the next level, win menu only or dev key pressed (=)
	public void OnNextLevelClicked()
	{
		// check if the next level even exists
		if (ResourceLoader.Exists($"res://Scenes/Levels/world{GameManager.currentWorld}/level{GameManager.currentLevel + 1}.tscn"))
		{
			Engine.TimeScale = 1;
			GameManager.currentLevel++;

			GetTree().ChangeSceneToFile($"res://Scenes/Levels/world{GameManager.currentWorld}/level{GameManager.currentLevel}.tscn");
			Songmixer.PlaySong(GameManager.currentWorld);
		}
		else
		{
			OnNextWorldClicked();
		}
	}

	public void OnNextWorldClicked()
	{
		// check if the next level even exists
		if (ResourceLoader.Exists($"res://Scenes/Levels/world{GameManager.currentWorld + 1}/level{1}.tscn"))
		{
			Engine.TimeScale = 1;
			GameManager.currentWorld++;
			GameManager.currentLevel = 1;

			GetTree().ChangeSceneToFile($"res://Scenes/Levels/world{GameManager.currentWorld}/level{GameManager.currentLevel}.tscn");
			Songmixer.PlaySong(GameManager.currentWorld);
		}
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
		Songmixer.PlaySong(9);
	}
}
