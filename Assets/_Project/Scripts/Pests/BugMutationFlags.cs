// Yaratığın çalışma anında kazandığı mutasyonlar (bayrak olarak birleşebilir). Yalnızca veridir: değer ve baskı
// hesabını değiştirmez. ScriptableObject'larda saklanmaz, kaydedilmez.
[System.Flags]
public enum BugMutationFlags
{
    None  = 0,
    Giant = 1 << 0,
}
