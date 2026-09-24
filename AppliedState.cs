namespace K86LayoutLight;
internal sealed class AppliedState
{
    private (int Layer, int Brightness)? sent;
    public bool Needs(int layer, int brightness) => sent != (layer, brightness);
    public void Mark(int layer, int brightness) => sent = (layer, brightness);
    public void Invalidate() => sent = null;
}
