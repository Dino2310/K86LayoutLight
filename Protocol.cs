namespace K86LayoutLight;
internal static class Protocol
{
    public static byte[] LayerPacket(int layer, int brightness, int speed = 5)
    {
        if (layer < 1 || layer > 2 || brightness < 1 || brightness > 4 || speed < 0 || speed > 255) 
            throw new ArgumentOutOfRangeException();

        // ID 1168: speed=5 matches vendor USB trace 2026-09-17 exactly.
        // Report ID at [0]; 64-byte ROYUAN packet starts at [1].
        // UI layer 1/2 corresponds to zero-based USERPIC slot 0/1.
        byte[] b = new byte[65]; 
        b[1] = 7; 
        b[2] = 13;
        b[3] = (byte)speed;
        b[4] = (byte)brightness;
        b[5] = (byte)((layer - 1) << 4);
        b[6] = 0;
        b[7] = 200;
        b[8] = 200;

        int sum = 0;
        for (int i = 1; i <= 8; i++)
            sum += b[i];

        b[9] = (byte)(255 - (sum & 255));

        return b;
    }
}
