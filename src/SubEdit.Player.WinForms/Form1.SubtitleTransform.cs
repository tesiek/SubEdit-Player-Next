namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void RegisterSubtitleTimeScaleTransform(double scale, string description, bool isFullDocument)
    {
        _rememberedSubtitleTimeScaleTransform = SubtitleTimeScaleTransform.Register(
            _rememberedSubtitleTimeScaleTransform,
            scale,
            description,
            isFullDocument);
    }

    private void ClearRememberedSubtitleTimeScaleTransform()
    {
        _rememberedSubtitleTimeScaleTransform = null;
    }

    internal SubtitleTimeScaleTransform? RememberedSubtitleTimeScaleTransform =>
        _rememberedSubtitleTimeScaleTransform;
}
