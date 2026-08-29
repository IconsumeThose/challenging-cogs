using Godot;
#pragma warning disable CA1050
// manage the ui overlaid in the gameplay
public partial class Ui : Control
{
	[Export]
	public Label cogCountLabel,
		paradigmShiftCountLabel,
		levelInfoLabel,
		moveCountLabel;

	public Godot.Collections.Array<Color> liquidColors = [
		new("131363"),
		new("131363"),
		new("131363"),
		new("131363"),
		new("fd9c96"),
		new("9e3a8a"),
		new("131363"),
		new("131363"),
		new("131363"),
		new("131363")
	];

	[Export] public Vector2I staminaSegmentSize = new(28, 32);
	[Export] public Sprite2D staminaBar;

	[Export] public GameManager gameManager;

	public override void _Ready()
	{
		/** <summary>Disable UI and don't do anything else if in level select</summary> */
		if (gameManager.IsLevelSelect)
		{
			Visible = false;
			return;
		}

		var atlas = GD.Load<Texture2D>("res://Assets/Sprites/uisheet.png");
		var tile = ImageTexture.CreateFromImage(atlas.GetImage().GetRegion(new Rect2I(2, 64, 28, 32)));
		staminaBar.Texture = tile;
		staminaBar.Modulate = new(liquidColors[DataManager.currentWorld]);
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

	public void UpdateMoveCountLabel(int newCount)
	{
		moveCountLabel.Text = $"Moves: {newCount}";
	}

	public void UpdateStaminaBar(int newCount)
	{
		staminaBar.RegionRect = new Rect2(0, 0, staminaSegmentSize.X * newCount, staminaSegmentSize.Y);
	}
}
