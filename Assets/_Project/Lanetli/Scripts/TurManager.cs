using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Round flow for Lanetli Yuk: Ready -> Round -> Break -> Round ...
/// A round ends when the timer runs out or the tower topples (only once per round).
/// Items stay in the van after a round but never score twice; a toppled round scores 0.
/// </summary>
public class TurManager : MonoBehaviour
{
    public enum Phase { Ready, Round, Break, Idle }

    [Header("Embedded in PestCo (optional)")]
    [Tooltip("When on, a round only starts when the start key is pressed (instead of looping automatically).")]
    [SerializeField] private bool requireStartKey = false;
    [SerializeField] private Key startKey = Key.F;
    [Tooltip("If set, the player must be inside this zone to start a round.")]
    [SerializeField] private TruckZone startArea;
    [Tooltip("Pay the round score as money through GameManager.AddMoney.")]
    [SerializeField] private bool payMoney = false;
    [SerializeField] private float moneyPerPoint = 1f;

    [Header("Balance")]
    [Tooltip("Ready / round / break durations come from here.")]
    [SerializeField] private GameConfig config;

    [Header("References")]
    [SerializeField] private WobblyTowerManager tower;
    [Tooltip("Vacuum is disabled outside of rounds.")]
    [SerializeField] private VacuumGun vacuum;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text centerText;

    public Phase CurrentPhase { get; private set; }
    public int RoundNumber { get; private set; } = 1;
    public int RoundScore { get; private set; }
    public int TotalScore { get; private set; }
    public float PhaseTimeLeft => _phaseTimer;

    private float _phaseTimer;

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogWarning("[TurManager] GameConfig is not assigned, using default values.", this);
            config = ScriptableObject.CreateInstance<GameConfig>();
        }
    }

    private void OnEnable()
    {
        if (tower != null)
        {
            tower.OnTowerToppled += HandleTowerToppled;
        }
    }

    private void OnDisable()
    {
        if (tower != null)
        {
            tower.OnTowerToppled -= HandleTowerToppled;
        }
    }

    private void Start()
    {
        RoundNumber = 1;
        if (requireStartKey)
        {
            EnterIdle();
            return;
        }

        EnterPhase(Phase.Ready, config.readyDuration);
        SetVacuum(false);
    }

    private void Update()
    {
        if (CurrentPhase == Phase.Idle)
        {
            UpdateIdle();
            return;
        }

        _phaseTimer -= Time.deltaTime;
        RefreshUi();

        if (_phaseTimer > 0f)
        {
            return;
        }

        switch (CurrentPhase)
        {
            case Phase.Ready:
                StartRound();
                break;
            case Phase.Round:
                EndRound(false);
                break;
            case Phase.Break:
                RoundNumber++;
                if (requireStartKey)
                {
                    EnterIdle();
                }
                else
                {
                    StartRound();
                }

                break;
        }
    }

    // Waiting for the start key (embedded mode). The HUD stays empty except for a hint inside the start area.
    private void EnterIdle()
    {
        CurrentPhase = Phase.Idle;
        _phaseTimer = 0f;
        if (timerText != null)
        {
            timerText.text = "";
        }

        if (centerText != null)
        {
            centerText.text = "";
        }

        SetVacuum(true);
    }

    private void UpdateIdle()
    {
        bool canStart = startArea == null || startArea.IsPlayerInside;
        if (centerText != null)
        {
            string hint = canStart ? $"{startKey}: yük taşımayı başlat" : "";
            if (centerText.text != hint)
            {
                centerText.text = hint;
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (canStart && keyboard != null && keyboard[startKey].wasPressedThisFrame)
        {
            EnterPhase(Phase.Ready, config.readyDuration);
            SetVacuum(false);
        }
    }

    private void StartRound()
    {
        if (tower != null)
        {
            tower.StartNewRound();
        }

        RoundScore = 0;
        EnterPhase(Phase.Round, config.roundDuration);
        SetVacuum(true);
    }

    private void HandleTowerToppled()
    {
        // The tower event can fire in any phase; only the first one during a round ends it.
        if (CurrentPhase == Phase.Round)
        {
            EndRound(true);
        }
    }

    private void EndRound(bool toppled)
    {
        if (CurrentPhase != Phase.Round)
        {
            return;
        }

        int delivered = CountDeliveredItems();
        RoundScore = toppled ? 0 : CountTowerScore();
        TotalScore += RoundScore;
        LogTelemetry(delivered, toppled);
        EnterPhase(Phase.Break, config.breakDuration);
        SetVacuum(false);

        string detail = toppled ? "Kule devrildi! Puan: 0" : $"Bu tur: {RoundScore} puan";
        string money = PayRoundMoney();
        if (centerText != null)
        {
            centerText.text = $"Tur {RoundNumber} bitti\n{detail}{money}\nToplam skor: {TotalScore}";
        }
    }

    // Embedded mode: the round score is paid as PestCo money (1 point = moneyPerPoint dollars).
    private string PayRoundMoney()
    {
        if (!payMoney || RoundScore <= 0)
        {
            return "";
        }

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[TurManager] GameManager not found, money was not paid.", this);
            return "";
        }

        int amount = Mathf.RoundToInt(RoundScore * moneyPerPoint);
        GameManager.Instance.AddMoney(amount);
        return $"\n+${amount}";
    }

    // Delivered = items standing in the tower plus items that already broke this round (cursed vase).
    private int CountDeliveredItems()
    {
        if (tower == null)
        {
            return 0;
        }

        int count = tower.BankedItemCount;
        foreach (TowerItem item in tower.SettledItems)
        {
            if (item != null && !item.HasFallen)
            {
                count++;
            }
        }

        return count;
    }

    // Per-round balance log. Editor and development builds only.
    private void LogTelemetry(int delivered, bool toppled)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string reason = !toppled ? "sure bitti" : (tower != null && tower.RecentlyBrokeCursedItem ? "vazo" : "devrildi");
        Debug.Log($"[TELEMETRI] tur={RoundNumber} teslim={delivered} devrildi={(toppled ? "evet" : "hayir")} " +
                  $"sebep={reason} skor={RoundScore} toplam={TotalScore}");
#endif
    }

    private int CountTowerScore()
    {
        int sum = 0;
        if (tower == null)
        {
            return sum;
        }

        // Points of items that already broke this round (e.g. the cursed vase) still count.
        sum += tower.BankedScore;

        foreach (TowerItem item in tower.SettledItems)
        {
            if (item != null && !item.HasFallen)
            {
                sum += Mathf.RoundToInt(item.ItemValue);
            }
        }

        return sum;
    }

    private void EnterPhase(Phase phase, float duration)
    {
        CurrentPhase = phase;
        _phaseTimer = duration;
        if (centerText != null && phase == Phase.Round)
        {
            centerText.text = "";
        }
    }

    private void SetVacuum(bool on)
    {
        if (vacuum != null)
        {
            vacuum.enabled = on;
        }
    }

    private void RefreshUi()
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, _phaseTimer));

        if (timerText != null)
        {
            timerText.text = CurrentPhase == Phase.Round
                ? $"Tur {RoundNumber}  {seconds}\nSkor: {TotalScore}"
                : $"Tur {RoundNumber}\nSkor: {TotalScore}";
        }

        if (centerText != null && CurrentPhase == Phase.Ready)
        {
            centerText.text = $"HAZIR\n{seconds}";
        }
    }
}
