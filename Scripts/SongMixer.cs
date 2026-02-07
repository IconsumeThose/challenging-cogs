using Godot;
using System;
// hi aaron
// hi sammy
public partial class SongMixer : AudioStreamPlayer
{
	public static Song currentSong = 0;
	public enum Song
	{
		world1 = 1, 
		world2 = 2, 
		world3 = 3, 
		world4 = 4, 
		world5 = 5, 
		world6 = 6, 
		world7 = 7, 
		world8 = 8,
		mainMenu = 9
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



	public static void PlaySong(Song songToPlay)
	{
		if (currentSong == songToPlay)
		{
			return;
		}
		switch (songToPlay)
		{
			case Song.world1:
				instance.Stream = instance.world1;
				break;
			case Song.world2:
				instance.Stream = instance.world2;
				break;
			case Song.world3:
				instance.Stream = instance.world3;
				break;
			case Song.world4:
				instance.Stream = instance.world4;
				break;
			case Song.world5:
				instance.Stream = instance.world5;
				break;
			case Song.world6:
				instance.Stream = instance.world6;
				break;
			case Song.world7:
				instance.Stream = instance.world7;
				break;
			case Song.world8:
				instance.Stream = instance.world8;
				break;
			case Song.mainMenu:
				instance.Stream = instance.mainMenu;
				break;
		}

		currentSong = songToPlay;
		instance.Play();
	}
}
