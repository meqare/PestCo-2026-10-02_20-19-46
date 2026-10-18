/// <summary>
/// Açılıp kapanabilen mobilya (dolap kapağı, çekmece). Oyuncu E ile, haşereler ise kod üzerinden açar.
/// </summary>
public interface IOpenableFurniture
{
    /// <summary>Şu an açık mı?</summary>
    bool IsOpen { get; }

    /// <summary>Açılma/kapanma animasyonunun süresi (sn).</summary>
    float OpenDuration { get; }

    /// <summary>Açar (zaten açıksa bir şey yapmaz).</summary>
    void Open();

    /// <summary>Kapatır (zaten kapalıysa bir şey yapmaz).</summary>
    void Close();
}
