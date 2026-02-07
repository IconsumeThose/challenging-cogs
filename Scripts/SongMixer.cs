using Godot;
using System;
// hi aaron
// hi sammy
public partial class SongMixer : AudioStreamPlayer
{
	public static int currentSong = 0;
	public enum Song
	{
		world1, world2, world3, world4, world5, world6, world7, world8, mainMenu
	}
	private static SongMixer instance;
	[Export] private AudioStream world1, world2, world3, world4, world5, world6, world7, world8, mainMenu; // now THIS makes it so you file drop in rogot
	public static void SetBusVolume(string bus, float volume) // adding this to settings sometime
	{
		int busIndex = AudioServer.GetBusIndex(bus);
		AudioServer.SetBusVolumeDb(busIndex, volume);
	}

	public override void _Ready()
	{
		instance = this; // idk what this does
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
				break;
			case 2:
				instance.Stream = instance.world2;
				break;
			case 3:
				instance.Stream = instance.world3;
				break;
			case 4:
				instance.Stream = instance.world4;
				break;
			case 5:
				instance.Stream = instance.world5;
				break;
			case 6:
				instance.Stream = instance.world6;
				break;
			case 7:
				instance.Stream = instance.world7;
				break;
			case 8:
				instance.Stream = instance.world8;
				break;
			case 9:
				instance.Stream = instance.mainMenu;
				break;
		}
		currentSong = songToPlay;
	}
}
