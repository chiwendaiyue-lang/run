using UnityEngine;

public class PlayerData
{
    public static PlayerData instance;

    public float masterVolume = float.MinValue;
    public float musicVolume = float.MinValue;
    public float masterSFXVolume = float.MinValue;

    private const string k_MasterVolumeKey = "master_volume";
    private const string k_MusicVolumeKey = "music_volume";
    private const string k_MasterSfxVolumeKey = "master_sfx_volume";

    public static void Create()
    {
        if (instance != null)
        {
            return;
        }

        instance = new PlayerData();

        if (PlayerPrefs.HasKey(k_MasterVolumeKey))
        {
            instance.masterVolume = PlayerPrefs.GetFloat(k_MasterVolumeKey);
            instance.musicVolume = PlayerPrefs.GetFloat(k_MusicVolumeKey, 0f);
            instance.masterSFXVolume = PlayerPrefs.GetFloat(k_MasterSfxVolumeKey, 0f);
        }
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(k_MasterVolumeKey, masterVolume);
        PlayerPrefs.SetFloat(k_MusicVolumeKey, musicVolume);
        PlayerPrefs.SetFloat(k_MasterSfxVolumeKey, masterSFXVolume);
        PlayerPrefs.Save();
    }
}
