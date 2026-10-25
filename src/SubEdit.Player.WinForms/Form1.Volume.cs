namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void AdjustVolume(int wheelDelta)
    {
        _wheelSeekDeltaAccumulator.Reset();
        var direction = Math.Sign(wheelDelta);
        if (direction != 0)
        {
            _volumeSlider.Value = Math.Clamp(
                _volumeSlider.Value + (direction * 5),
                _volumeSlider.Minimum,
                _volumeSlider.Maximum);
            ShowVolumeOsd();
        }
    }

    private void SetVolume(int volume)
    {
        _settings.Volume = volume;
        _playback.VolumePercent = volume;
        if (volume > 0)
        {
            _volumeBeforeMute = volume;
            _settings.LastAudibleVolume = volume;
        }

        _volumeButton.Checked = volume == 0;
    }

    private void ToggleMute()
    {
        var mute = _volumeSlider.Value > 0;
        _volumeSlider.Value = mute
            ? 0
            : Math.Clamp(_volumeBeforeMute, _volumeSlider.Minimum, _volumeSlider.Maximum);
        if (mute)
        {
            ShowPlaybackOsd("Wyciszony");
        }
        else
        {
            ShowVolumeOsd();
        }
    }
}
