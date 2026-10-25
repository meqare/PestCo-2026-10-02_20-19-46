using System.Collections.Generic;
using UnityEngine;

public enum CageSaleStatus
{
    Success,
    NothingToSell,  // satılacak yaratık yok (boş fanus ya da zaten satılmış)
    TooLate,        // fanus BreachPending: satış engelli
    Failed          // GameManager yok vb.: hiçbir şey satılmadı, para verilmedi
}

public readonly struct CageSaleResult
{
    public readonly CageSaleStatus status;
    public readonly int payout;
    public readonly int count;

    public CageSaleResult(CageSaleStatus status, int payout, int count)
    {
        this.status = status;
        this.payout = payout;
        this.count = count;
    }
}

// Fanus satışının TEK güvenilir yolu: panel (SAT) ve raf (BugShelf.SellAll / PestPC) aynı işlemi kullanır.
// Durumsuz statik yardımcıdır (yönetici/singleton yok). Fiyat, mevcut BugCage.TotalValue()'dur; yeni formül yoktur.
//
// Sıra (aynı karede, bölme bölme tutarlı):
//   1. Uygun bölmeleri seç (dolu, BreachPending değil, şu an satılmıyor) ve toplam değeri HESAPLA.
//   2. GameManager yoksa hiçbir şey yapma (yaratıklar silinmez, para yok).
//   3. Her bölmenin içeriğini sil (BugCage.SellContents; çifte satışı IsSelling korur).
//   4. Silinenlerin değerini tek seferde GameManager.AddMoney(int) ile öde (int taşmasına karşı sınırlı).
public static class CageSaleService
{
    // Bölme kırılma beklerken satış engellidir.
    public static bool IsBlocked(BugCage cage)
    {
        if (cage == null) return true;
        CageCriticalController critical = cage.GetComponent<CageCriticalController>();
        return critical != null && critical.IsSaleBlocked;
    }

    // Bölmenin şu an satılabilir değeri (engelliyse ya da boşsa 0).
    public static float PreviewValue(BugCage cage)
    {
        if (cage == null || cage.IsBroken || cage.IsSelling || cage.Count == 0 || IsBlocked(cage)) return 0f;
        float value = cage.TotalValue();
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    public static int PreviewPayout(BugCage cage)
    {
        return ToPayout(PreviewValue(cage));
    }

    public static CageSaleResult SellCage(BugCage cage)
    {
        return SellCages(new[] { cage });
    }

    public static CageSaleResult SellCages(IEnumerable<BugCage> cages)
    {
        var eligible = new List<BugCage>();
        float totalValue = 0f;
        bool anyBlocked = false;

        foreach (BugCage cage in cages)
        {
            if (cage == null || cage.IsBroken || cage.IsSelling || cage.Count == 0) continue;
            if (IsBlocked(cage))
            {
                anyBlocked = true;
                continue;
            }
            eligible.Add(cage);
        }

        if (eligible.Count == 0)
            return new CageSaleResult(anyBlocked ? CageSaleStatus.TooLate : CageSaleStatus.NothingToSell, 0, 0);

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("CageSaleService: GameManager sahnede yok, satış yapılmadı (yaratıklar silinmedi).");
            return new CageSaleResult(CageSaleStatus.Failed, 0, 0);
        }

        // Önce değeri al (silmeden), sonra sil; yalnızca gerçekten silinen bölmenin değeri ödenir.
        int soldCount = 0;
        foreach (BugCage cage in eligible)
        {
            float value = PreviewValue(cage);
            int removed = cage.SellContents();
            if (removed <= 0) continue;

            soldCount += removed;
            totalValue += value;
        }

        if (soldCount == 0) return new CageSaleResult(CageSaleStatus.NothingToSell, 0, 0);

        int payout = ToPayout(totalValue);
        if (payout > 0) gm.AddMoney(payout);

        Debug.Log($"[Fanus] {soldCount} yaratık satıldı, kazanç: ${payout}");
        return new CageSaleResult(CageSaleStatus.Success, payout, soldCount);
    }

    // Değeri güvenli bir tam sayıya çevirir (NaN/negatif 0, int.MaxValue üstü sınırlı).
    private static int ToPayout(float value)
    {
        if (float.IsNaN(value) || value <= 0f) return 0;
        return value >= int.MaxValue ? int.MaxValue : Mathf.RoundToInt(value);
    }
}
