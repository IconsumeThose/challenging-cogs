using Godot;
using System;

// manage the ui overlaid in the gameplay
public partial class Ui : Control
{
	[Export]
	public Label cogCountLabel,
		paradigmShiftCountLabel,
		levelInfoLabel;

	[Export] public GameManager gameManager;

	public override void _Ready()
	{
		UpdateCogCountLabel(0);
		UpdateParadigmShiftCountLabel(gameManager.maxParadigmShifts);

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
}
