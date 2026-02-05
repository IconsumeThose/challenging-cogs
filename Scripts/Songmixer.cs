using Godot;
using System;
// hi aaron

public partial class Songmixer : AudioStreamPlayer
{
	public static int currentSong = 0;
	public enum Song
	{
		world1, world2, world3, world4, world5, world6, world7, world8, mainmenu
	}
	private static Songmixer instance;
	[Export] private AudioStream world1, world2, world3, world4, world5, world6, world7, world8, mainmenu; // now THIS makes it so you file drop in rogot
	public static void SetBusVolume(string bus, float volume) // adding this to settings sometime
	{
		int busIndex = AudioServer.GetBusIndex(bus);
		AudioServer.SetBusVolumeDb(busIndex, volume);
	}

	public override void _Ready()
	{
		instance = this; // idk what this does
		Console.WriteLine("test");
	}
	public static void PlaySong(int songToPlay)
	{
		if (currentSong == songToPlay)
		{
			return;
		}
		switch (songToPlay)
		{
			case 1:
				instance.Stream = instance.world1;
				instance.Play();
				currentSong = 1;
				break;
			case 2:
				instance.Stream = instance.world2;
				instance.Play();
				currentSong = 2;
				break;
			case 3:
				instance.Stream = instance.world3;
				instance.Play();
				currentSong = 3;
				break;
			case 4:
				instance.Stream = instance.world4;
				instance.Play();
				currentSong = 4;
				break;
			case 5:
				instance.Stream = instance.world5;
				instance.Play();
				currentSong = 5;
				break;
			case 6:
				instance.Stream = instance.world6;
				instance.Play();
				currentSong = 6;
				break;
			case 7:
				instance.Stream = instance.world7;
				instance.Play();
				currentSong = 7;
				break;
			case 8:
				instance.Stream = instance.world8;
				instance.Play();
				currentSong = 8;
				break;
			case 9:
				instance.Stream = instance.mainmenu;
				instance.Play();
				currentSong = 9;
				break;
				
		}
	}
}
