using Godot;
using System;

public partial class Menu : Control
{
	/** <summary>List of UI that changes color based on current world</summary> */
	[Export] public Godot.Collections.Array<CanvasItem> modulatableMenuItem = [];

	/** <summary>colors used for each world for UI, usually derived from cog color</summary> */
	[Export]
	public Godot.Collections.Array<Color> worldColors = [
		new("000000"),
		new("2e3996"),
		new("cc1818"),
		new("89d7ff"),
		new("e06797"),
		new("228217"),
		new("000000"),
		new("000000"),
		new("000000"),
		new("000000")
	];

	public Godot.Collections.Array<string> worldNames = [
		"T",
		"Banana Beach",
		"Mechanical Machinery",
		"Gloomy Glacier",
		"Tasty Treats",
		"Fungus Forest",
		"Abandoned Amphitheater",
		"Urban Underground",
		"Chaos Cosmos"
	];

	/** <summary>background images for each world</summary> */
	[Export] public Godot.Collections.Array<CompressedTexture2D> worldBackgrounds = [];

	[Export] public float menuItemLightenAmount = 0.8f;
	[Export] public Sprite2D background;
	
	[Export] public CheckBox confirmDeleteSave; 

	[Export] public CheckButton holdToMoveSwitch,
		holdToResetSwitch,
		fullscreenSwitch;
	
	[Export] public Label worldLabel;

	[Export] public Slider masterVolumeSlider,
		musicVolumeSlider,
		SFXVolumeSlider;

	[Export]
	public Button nextWorldButton,
		previousWorldButton;

	/** <summary>Don't call outside of MoveCountsShow</summary> */
	private bool moveCountsShown = false;

	/** <summary>Handles updating all move count labels when updated</summary> */
	private bool MoveCountsShown
	{
		get { return moveCountsShown; }
		
		set
		{
			moveCountsShown = value;

			for (int i = 1; i <= 15; i++)
			{
				// referring to i directly is bad because its a reference and fails to bind pressed correctly
				int currentLevel = i;

				// find all the necessary sub-components to work with
				SubViewportContainer subViewportContainer = GetNode<SubViewportContainer>($"LevelPreview{currentLevel}/SubViewportContainer");
				Label moveCountLabel = subViewportContainer.GetParent().GetNode<Label>("MoveCount");

				// check if the level actually exists
				bool levelExists = ResourceLoader.Exists($"res://Scenes/Levels/world{DataManager.currentWorld}/level{currentLevel}.tscn");

				if (levelExists)
				{
					moveCountLabel.Visible = moveCountsShown;
				}
			}
		}
	}

	public override void _Ready()
	{
		foreach (CanvasItem menuItem in modulatableMenuItem)
		{
			menuItem.SelfModulate = worldColors[DataManager.currentWorld];

			// darken the level
			menuItem.SelfModulate *= menuItemLightenAmount;

			if (menuItem is TextureButton button)
			{
				button.MouseEntered += () => OnMouseEntered(button.GetPath());
				button.MouseExited += () => OnMouseExited();
				button.FocusEntered += () => OnFocusEntered(button.GetPath());
				button.FocusExited += () => OnFocusExited(button.GetPath());
			}
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
			worldLabel.Text = $"World {DataManager.currentWorld}: {worldNames[DataManager.currentWorld]}";
			// fill all the level previews
			for (int i = 1; i <= 15; i++)
			{
				// referring to i directly is bad because its a reference and fails to bind pressed correctly
				int currentLevel = i;

				// find all the necessary sub-components to work with
				SubViewportContainer subViewportContainer = GetNode<SubViewportContainer>($"LevelPreview{currentLevel}/SubViewportContainer");
				TextureButton button = subViewportContainer.GetParent().GetNode<TextureButton>("TextureButton");
				Label levelLabel = subViewportContainer.GetParent().GetNode<Label>("LevelNumber"),
					moveCountLabel = subViewportContainer.GetParent().GetNode<Label>("MoveCount");

				// check if the level actually exists
				bool levelExists = ResourceLoader.Exists($"res://Scenes/Levels/world{DataManager.currentWorld}/level{currentLevel}.tscn");

				if (!levelExists)
				{
					button.FocusMode = FocusModeEnum.None;
					levelLabel.Text = "";
				}
				// don't load locked levels
				else if (DataManager.currentWorld > DataManager.savedWorld || (DataManager.currentWorld == DataManager.savedWorld && currentLevel > DataManager.savedLevel))
				{
					subViewportContainer.SelfModulate = new("22222222");
					
					levelLabel.Text = "🔒";
					
					button.FocusMode = FocusModeEnum.None;
					continue;
				}
		
				if (levelExists)
				{
					// bind all the necessary actions for the custom buttons
					button.Pressed += () => OnLevelButtonPressed(currentLevel);
					button.MouseEntered += () => OnMouseEntered(subViewportContainer.GetPath());
					button.MouseExited += () => OnMouseExited();
					button.FocusEntered += () => OnFocusEntered(subViewportContainer.GetPath());
					button.FocusExited += () => OnFocusExited(subViewportContainer.GetPath());

					moveCountLabel.Visible = false;

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
					subViewportContainer.SelfModulate *= menuItemLightenAmount;

					levelLabel.Text = $"{currentLevel}";

					// only show move count if its less than the default
					if (DataManager.moveCounts[DataManager.currentWorld, currentLevel - 1] < int.MaxValue)
						moveCountLabel.Text = $"Least Moves: {DataManager.moveCounts[DataManager.currentWorld, currentLevel - 1]}";

					levelScene.Scale = new(1f / 6f, 1f / 6f);
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
		else if (Name == "SettingsMenu")
		{
			if (DataManager.holdToMove)
			{
				// set hold to move switch to on if the setting is on
				holdToMoveSwitch.SetPressedNoSignal(true);
			}

			if (DataManager.holdToReset)
			{
				// set hold to move switch to on if the setting is on
				holdToResetSwitch.SetPressedNoSignal(true);
			}

			if (DataManager.IsFullscreen)
			{
				fullscreenSwitch.SetPressedNoSignal(true);
			}

			// set all volume sliders to match their current volumes
			masterVolumeSlider.Value = AudioServer.GetBusVolumeLinear(AudioServer.GetBusIndex("Master")) * 100;
			musicVolumeSlider.Value = AudioServer.GetBusVolumeLinear(AudioServer.GetBusIndex("Music")) * 100;
			SFXVolumeSlider.Value = AudioServer.GetBusVolumeLinear(AudioServer.GetBusIndex("SFX")) * 100;

			GetNode<Slider>("VBoxContainer/MasterVolumeSlider").GrabFocus();
		}
		else
		{
			SongMixer.PlaySong((SongMixer.Song)DataManager.currentWorld);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Input.IsActionJustPressed("ToggleFullscreen"))
		{
			OnFullscreenToggled(!DataManager.IsFullscreen);
		}
		if (Input.IsActionJustPressed("DEBUGResetLeastMoves"))
		{
			GD.Print(DataManager.currentWorld + " " + DataManager.currentLevel);
			DataManager.moveCounts[DataManager.currentWorld, DataManager.currentLevel - 1] = int.MaxValue;
			DataManager.SaveGame();
		}
		if (Name != "LevelSelect")
			return;

		if (Input.IsActionJustPressed("ToggleMoveCounts"))
		{
			MoveCountsShown = !MoveCountsShown;
		}
	}

	public enum SliderType
	{
		masterVolume = 0,
		musicVolume = 1,
		SFXVolume = 2
	}

	// update volume when the slider is moved
	public void OnSliderChanged(float value, SliderType slider)
	{
		string busName = "";

		switch (slider)
		{
			case SliderType.masterVolume:
				busName = "Master";
				break;
			case SliderType.musicVolume:
				busName = "Music";
				break;
			case SliderType.SFXVolume:
				busName = "SFX";
				break;
		}

		int busIndex = AudioServer.GetBusIndex(busName);

		AudioServer.SetBusVolumeLinear(busIndex, value / 100);
	}

	// make the confirmation box visible
	public void OnDeleteSavePressed()
	{
		confirmDeleteSave.Visible = true;
	}

	// delete the save if confirmed
	public void OnDeleteSaveConfirmed(bool toggledOn)
	{
		if (toggledOn)
		{
			DataManager.ResetSave();
			confirmDeleteSave.Text = "Save Deleted!";
			OnSettingsPressed();
		}
	}

	public void OnDefaultSettingsPressed()
	{
		DataManager.DefaultSettings();
		OnSettingsPressed();
	}

	public void OnSettingsPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/settings_menu.tscn");
	}

	public void OnPreviousWorldPressed()
	{
		DataManager.currentWorld--;
		OnLevelSelectPressed();
	}

	public void OnNextWorldPressed()
	{
		DataManager.currentWorld++;
		OnLevelSelectPressed();
	}

	public void OnLevelButtonPressed(int level)
	{
		DataManager.LoadLevel(level: level);
	}

	public void OnMouseEntered(NodePath nodePath)
	{
		CanvasItem menuItem = GetNode<CanvasItem>(nodePath);

		TextureButton button = null;

		// show move count
		if (menuItem is SubViewportContainer)
		{
			button = menuItem.GetParent().GetNode<TextureButton>("TextureButton");
		}
		else if (menuItem is TextureButton textureButton)
		{
			button = textureButton;
		}

		Control focusOwner = GetViewport().GuiGetFocusOwner();
		if (focusOwner != button)
		{
			GetViewport().GuiReleaseFocus();
		}

		button?.GrabFocus();
	}
	
	public void OnFocusEntered(NodePath nodePath)
	{
		CanvasItem menuItem = GetNode<CanvasItem>(nodePath);

		menuItem.SelfModulate *= 1 / menuItemLightenAmount;

		// show move count
		if (menuItem is SubViewportContainer)
		{
			Label moveCountLabel = menuItem.GetParent().GetNode<Label>("MoveCount");
			moveCountLabel.Visible = true;
		}
	}
	
	public void OnMouseExited()
	{
		
	}

	public void OnFocusExited(NodePath nodePath)
	{
		CanvasItem menuItem = GetNode<CanvasItem>(nodePath);

		menuItem.SelfModulate *= menuItemLightenAmount;

		if (menuItem is SubViewportContainer)
		{
			Label moveCountLabel = menuItem.GetParent().GetNode<Label>("MoveCount");
			moveCountLabel.Visible = MoveCountsShown;
		}
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

	public void OnHoldToMoveToggled(bool toggledOn)
	{
		DataManager.holdToMove = toggledOn;
	}

	public void OnHoldToResetToggled(bool toggledOn)
	{
		DataManager.holdToReset = toggledOn;
	}

	public void OnFullscreenToggled(bool toggledOn)
	{
		GD.Print(toggledOn);
		DataManager.SetFullScreen(toggledOn);
		
		if (Name == "SettingsMenu")
		{
			OnSettingsPressed();
		}
	}

	/** <summary>Closes the game</summary> */
	public void OnClosePressed()
	{
		GetTree().Quit();
	}

	public void OnPlayPressed()
	{
		DataManager.LoadLevel(DataManager.savedWorld, DataManager.savedLevel);
	}

	public void OnLevelSelectPressed()
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
		if (Name == "SettingsMenu")
		{
			DataManager.SaveGame(true, false);
		}

		Engine.TimeScale = 1;
		GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
	}
}
