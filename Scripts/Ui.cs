using Godot;
using System;

public partial class Ui : Control
{
	[Export]
	public Label cogCountLabel,
		paradigmShiftCountLabel;

	[Export] public GameManager gameManager;

	public override void _Ready()
	{
		UpdateCogCountLabel(0);
		UpdateParadigmShiftCountLabel(gameManager.maxParadigmShifts);
	}

	public void UpdateCogCountLabel(int newCount)
	{
		cogCountLabel.Text = $"Cogs Challenged: {newCount} / {gameManager.totalNumberOfCogs}";
	}

	public void UpdateParadigmShiftCountLabel(int newCount)
	{
		paradigmShiftCountLabel.Text = $"Paradigm Shifts Left: {newCount} / {gameManager.maxParadigmShifts}";
	}

}
