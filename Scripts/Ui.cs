using Godot;
using System;

// manage the ui overlaid in the gameplay
public partial class Ui : Control
{
	[Export]
	public Label cogCountLabel,
		paradigmShiftCountLabel,
		levelInfoLabel;

	[Export] public Vector2I staminaSegmentSize = new(471, 471);
	[Export] public Sprite2D staminaBar;

	[Export] public GameManager gameManager;

	public override void _Ready()
	{
		/** <summary>Disable UI and don't do anything else if in level select</summary> */
		if (gameManager.IsLevelSelect())
		{
			Visible = false;
			return;
		}

		UpdateCogCountLabel(0);
		UpdateParadigmShiftCountLabel(gameManager.maxParadigmShifts);
		UpdateStaminaBar(gameManager.maxStamina);

		gameManager.CalculateCurrentWorldAndLevel();
		levelInfoLabel.Text = $"World {DataManager.currentWorld}-{DataManager.currentLevel} \"{gameManager.levelName}\"";
	}
	// useless line

	public void UpdateCogCountLabel(int newCount)
	{
		cogCountLabel.Text = $"Cogs Challenged: {newCount} / {gameManager.TotalNumberOfCogs}";
	}

	public void UpdateParadigmShiftCountLabel(int newCount)
	{
		paradigmShiftCountLabel.Text = $"Paradigm Shifts Left: {newCount} / {gameManager.maxParadigmShifts}";
	}

	public void UpdateStaminaBar(int newCount)
	{
		staminaBar.RegionRect = new Rect2(0, 0, staminaSegmentSize.Y * newCount, staminaSegmentSize.Y);
	}
}
