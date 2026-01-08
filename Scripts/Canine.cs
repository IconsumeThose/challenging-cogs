using Godot;
using System;

public partial class Canine : CharacterBody2D
{
	// the distance the canine moves every time
	[Export] public float distance = 2400;
	
	// the direction the player moved last frame
	private Vector2 lastMovementDirection = Vector2.Zero;
	public override void _PhysicsProcess(double delta)
	{
		// read the inputs of the player
		Vector2 movementDirection = movementDirection = Input.GetVector("Left", "Right", "Up", "Down");
		GD.Print(movementDirection);
		// handle diagonal inputs (values get normalized so x will be ~0.7)
		if (Math.Abs(movementDirection.X) > 0 && Math.Abs(movementDirection.X) < 1)
		{
			// randomly select which input to use, if 1 is generated, select horizontal, otherwise select the vertical
			bool horizontal = GD.RandRange(0, 1) == 1;

			if (horizontal)
			{
				// keep the direction of the x component but make its magnitude 1
				movementDirection = new(movementDirection.X / Math.Abs(movementDirection.X), 0);
			}
			else
			{
				// keep the direction of the y component but make its magnitude 1
				movementDirection = new(0, movementDirection.Y / Math.Abs(movementDirection.X));
			}
		}

		// only move if last frame there was no input so you are forced to tap every time
		if (lastMovementDirection == Vector2.Zero && movementDirection != Vector2.Zero)
		{
			Velocity = movementDirection * distance;
			MoveAndSlide();
		}
		
		lastMovementDirection = movementDirection;
	}

}
