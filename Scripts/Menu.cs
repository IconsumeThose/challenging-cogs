using Godot;
using System;

public partial class Menu : Control
{
	/** <summary>List of UI that changes color based on current world</summary> */
	[Export] public Godot.Collections.Array<CanvasItem> modulatableMenuItem = [];

	/** <summary>colors used for each world for UI, usually derived from cog color</summary> */
	[Export] public Godot.Collections.Array<Color> worldColors = [
		new("000000"),
		new("2e3996"),
		new("cc1818"),
		new("89d7ff"),
		new("e06797"),
		new("000000"),
		new("000000"),
		new("000000"),
		new("000000"),
		new("000000")
	];

	/** <summary>background images for each world</summary> */
	[Export] public Godot.Collections.Array<CompressedTexture2D> worldBackgrounds = [];

	[Export] public float menuItemLightenAmount = 0.3f; 
	[Export] public Sprite2D background;
	[Export] public Button nextWorldButton,
		previousWorldButton;
		
	public override void _Ready()
	{
		foreach (CanvasItem menuItem in modulatableMenuItem)
		{
			menuItem.SelfModulate = worldColors[DataManager.currentWorld];
		}

		if (Name == "MainMenu")
		{
			SongMixer.PlaySong(SongMixer.Song.mainMenu);
			GetNode<Button>("MainMenuVBox/PlayButton").GrabFocus();
		}
		else if (Name == "LevelSelect")
		{
			// grab focus of back button just in case it can't find a level
			GetNode<TextureButton>("BackButton").GrabFocus();

			// set background to the worlds background
			background.Texture = worldBackgrounds[DataManager.currentWorld];

			// fill all the level previews
			for (int i = 1; i <= 15; i++)
			{
				// referring to i directly is bad because its a reference and fails to bind pressed correctly
				int currentLevel = i;

				// first check if the level actually exists
				if (ResourceLoader.Exists($"res://Scenes/Levels/world{DataManager.currentWorld}/level{currentLevel}.tscn"))
				{
					// find all the necessary sub-components to work with
					SubViewportContainer subViewportContainer = GetNode<SubViewportContainer>($"LevelPreview{currentLevel}/SubViewportContainer");
					TextureButton button = subViewportContainer.GetParent().GetNode<TextureButton>("TextureButton");
					Label levelLabel = subViewportContainer.GetParent().GetNode<Label>("Label");

					// don't load locked levels
					if (DataManager.currentWorld > DataManager.savedWorld || (DataManager.currentWorld == DataManager.savedWorld && currentLevel > DataManager.savedLevel))
					{
						subViewportContainer.SelfModulate = new("22222222");
						levelLabel.Text = "🔒";
						button.FocusMode = FocusModeEnum.None;
						continue;
					}

					// bind all the necessary actions for the custom buttons
					button.Pressed += () => OnLevelButtonPressed(currentLevel);
					button.MouseEntered += () => OnMouseEntered(subViewportContainer.GetPath());
					button.MouseExited += () => OnMouseExited(subViewportContainer.GetPath());
					button.FocusEntered += () => OnMouseEntered(subViewportContainer.GetPath());
					button.FocusExited += () => OnMouseExited(subViewportContainer.GetPath());

					// move focus to saved level if on current world or first level if on previous world
					if (DataManager.currentWorld == DataManager.savedWorld && currentLevel == DataManager.savedLevel || DataManager.currentWorld != DataManager.savedWorld && currentLevel == 1)
					{
						button.GrabFocus();
					}

					// clone the level to put in the preview
					PackedScene packedLevelScene = GD.Load<PackedScene>($"res://Scenes/Levels/world{DataManager.currentWorld}/level{currentLevel}.tscn");
					Node2D levelScene = (Node2D)packedLevelScene.Instantiate();

					subViewportContainer.GetNode($"SubViewport").AddChild(levelScene);

					// darken the level
					subViewportContainer.SelfModulate *= menuItemLightenAmount * menuItemLightenAmount;

					levelLabel.Text = $"{currentLevel}";

					levelScene.Scale = new(1f/6f, 1f/6f);
				}
			}

			// don't show next world button if it isn't unlocked
			if (DataManager.currentWorld >= DataManager.savedWorld)
			{
				nextWorldButton.Visible = false;
			}

			// don't show previous world button if on first world
			if ((DataManager.currentWorld == 1 && !Input.IsActionPressed("Pause")) || DataManager.currentWorld < 1)
			{
				previousWorldButton.Visible = false;
			}
		}
		else
		{
			SongMixer.PlaySong((SongMixer.Song)DataManager.currentWorld);
		}
	}

	public void OnPreviousWorldPressed()
	{
		DataManager.currentWorld--;
		OnPlayPressed();
	}

	public void OnNextWorldPressed()
	{
		DataManager.currentWorld++;
		OnPlayPressed();
	}

	public void OnLevelButtonPressed(int level)
	{
		DataManager.LoadLevel(level: level);
	}

	public void OnMouseEntered(NodePath nodePath)
	{
		CanvasItem menuItem = GetNode<CanvasItem>(nodePath);

		menuItem.SelfModulate *= 1/menuItemLightenAmount;
	}

	public void OnMouseExited(NodePath nodePath)
	{
		CanvasItem menuItem = GetNode<CanvasItem>(nodePath);

		menuItem.SelfModulate *= menuItemLightenAmount;
	}

	/** <summary>Restart the level</summary> */
	public void OnRestartPressed()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile($"res://Scenes/Levels/world{DataManager.currentWorld}/level{DataManager.currentLevel}.tscn");
		SongMixer.PlaySong((SongMixer.Song)DataManager.currentWorld);
	}

	/** <summary>Take you to the next level, win menu only or dev key pressed (=)</summary> */
	public void OnNextLevelPressed()
	{
		DataManager.LoadLevel();
	}

	/** <summary>Dev key only to skip world</summary> */
	public void OnDevNextWorldPressed()
	{
		DataManager.LoadWorld(DataManager.currentWorld + 1);
	}

	/** <summary>Closes the game</summary> */
	public void OnClosePressed()
	{
		GetTree().Quit();
	}

	public void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/level_select_menu.tscn");
	}

	/** <summary>Specifically for the pause menu to unpause the game</summary> */
	public void OnContinuePressed()
	{
		Visible = false;
		Engine.TimeScale = 1;
	}

	public void OnUndoPressed()
	{
		Engine.TimeScale = 1;
		Visible = false;
		Cogito cogito = GetParent().FindChild("ScalingParent").FindChild("Cogito") as Cogito;
		cogito.Undo();
	}

	/** <summary>Take you back to the main menu</summary> */
	public void OnMainMenuPressed()
	{
		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
	}
}
