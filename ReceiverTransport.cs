namespace K86LayoutLight;
// ROYUAN 3151:4011 relay. Buffers include Windows report-ID byte at index 0.
internal sealed class ReceiverTransport
{
    readonly Action<byte[]> send;
    readonly Func<byte[]> read;
    readonly Action<int> delay;
    public ReceiverTransport(Action<byte[]> send, Func<byte[]> read, Action<int>? delay = null)
    {
        this.send = send;
        this.read = read;
        this.delay = delay ?? Thread.Sleep; 
    }
    public static byte[] Command(byte opcode, byte argument = 0)
    {
        var b = new byte[65];
        b[1] = opcode;
        b[2] = argument;
        b[8] = (byte)(255 - ((opcode + argument) & 255));
        return b;
    }
    byte[] Status()
    {
        send(Command(0xf7)); delay(20); var s = read();

        if (s.Length != 65 || s[1] > 1 || s[2] > 100 || s[4] > 1 || s[6] > 1 || s[7] > 3)
            throw new Exception("Приёмник вернул неподдерживаемый формат статуса.");

        if (s[4] != 0 || (s[7] & 1) == 0)
            throw new Exception("Приёмник не видит клавиатуру. Включи режим 2,4 ГГц и нажми любую клавишу.");

        return s;
    }
    void Wait(bool reply)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var s = Status();

            if (s[reply ? 1 : 6] == 1) 
                return;

            if (attempt != 4) 
                delay(100);
        }
        throw new TimeoutException(reply ? "Нет ответа клавиатуры через приёмник." : "Приёмник не готов передавать команды подсветки.");
    }
    public void Write(byte[] packet)
    {
        Wait(false);
        send(Command(0xf6, 10)); 
        send(packet);
        // A SET produces no reply. Never wait for reply-ready or repeat a successful write.
    }
    public bool TryWrite(byte[] packet, Func<bool> canSend)
    {
        if (!canSend()) 
            return false;

        Wait(false);
        // Input may resume while the radio receiver is becoming ready.

        if (!canSend()) 
            return false;

        send(Command(0xf6, 10));
        send(packet); 
        return true;
    }
    public byte[] Query(byte[] packet)
    {
        Write(packet);
        Wait(true); 
        send(Command(0xfc));
        delay(20); 
        var result = read();

        if (result.Length != 65 || result[1] != packet[1])
            throw new Exception("Неожиданный ответ клавиатуры через приёмник.");

        return result;
    }
}
